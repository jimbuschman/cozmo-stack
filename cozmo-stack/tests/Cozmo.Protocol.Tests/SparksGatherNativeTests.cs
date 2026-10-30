using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparksGatherNativeTests
{
    private static Activity Spark(Fixture f)=>NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="SparksGatherCubes");
    [Fact]
    public void ExactConfiguredOwnersAndOrderedDelegateAreBound()
    {
        using var f=new Fixture();
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["SparksBringCubeToBeacon"]);
        var think=Assert.IsType<ThinkAboutBeaconsBehavior>(f.Runtime.Bound["SparksThinkAboutBeacons"]);
        Assert.Equal(75f,think.BeaconRadiusMm);
        var spark=Assert.IsType<NativeSparkedActivity>(Spark(f).SparkedOwner);
        Assert.True(spark.CanExecute);
        var nested=Assert.IsType<Activity>(spark.Delegate);
        Assert.Equal("GatherCubes",nested.Id);
        Assert.IsType<NativeGatherCubesActivity>(nested.GatherOwner);
        Assert.Equal(new[]{"DriveOffCharger","KnockOverCubes","Hiking_RollCube","SparksThinkAboutBeacons","SparksBringCubeToBeacon","SparksLookInPlace"},nested.Chooser!.BehaviorIds);
    }
    [Fact]
    public void ActualObjectiveSubscriptionFiltersAndRetires()
    {
        using var f=new Fixture();var owner=Spark(f).SparkedOwner!;
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,10u);Assert.Equal(0u,owner.Count);
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,11u);Assert.Equal(1u,owner.Count);
        owner.Dispose();f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,11u);Assert.Equal(1u,owner.Count);
    }
    [Fact]
    public void SuppliedAdmissionPublishesOuterThenNestedAndClearsBeaconOnSelection()
    {
        using var f=new Fixture();
        f.M.Whiteboard.AddBeacon(f.Robot.GetCheckedMainPose(),75f);
        var published=new List<byte>();
        using var subscription=f.Robot.BehaviorPublicState.Subscribe(state=>published.Add(state.Activity));
        var manager=f.Robot.BehaviorManagerOwner!;manager.SetNativeRequestedSpark(38,1);
        f.Runtime.AdmitActivity("SparksGatherCubes");manager.SwitchToNativeRequestedSpark();
        Assert.Null(f.M.Whiteboard.GetActiveBeacon());
        Assert.Equal(new byte[]{20,4},published);
        Assert.Equal(4,f.Robot.BehaviorPublicState.State.Activity);
    }
    private sealed class Probe(string id,Func<bool> ready) : SteppedBehavior(id,"Probe")
    {
        internal int Calls;
        protected override void OnStart() {}
        public override bool IsRunnable(BehaviorContext context){Calls++;return ready();}
    }
    [Fact]
    public void PriorityDistinguishesFalseFromUnavailableAndEvaluatesEachEncounterOnce()
    {
        using var f=new Fixture();
        var first=new Probe("first",()=>false);
        var missing=new Probe("missing",()=>{SteppedBehavior.MarkReadinessUnavailable();return false;});
        var last=new Probe("last",()=>true);
        var chooser=new StrictPriorityChooser(new[]{"first","missing","last"},new Dictionary<string,IBehavior>{{"first",first},{"missing",missing},{"last",last}}){StopOnUnavailable=true};
        var result=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);
        Assert.True(result.InputUnavailable);Assert.Null(result.Behavior);
        Assert.Equal(1,first.Calls);Assert.Equal(1,missing.Calls);Assert.Equal(0,last.Calls);
        // An ordinary bool caller receives its existing false and does not change the next evaluation.
        Assert.False(missing.IsRunnable(f.Runtime.Context));
        var available=new Probe("available",()=>false).EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(available.Available);Assert.False(available.Runnable);
    }
    [Fact]
    public void NestedReadinessFramesDoNotOverwriteOuterEvaluation()
    {
        using var f=new Fixture();
        var inner=new Probe("inner",()=>{SteppedBehavior.MarkReadinessUnavailable();return false;});
        var outer=new Probe("outer",()=>{var result=inner.EvaluateHostReadiness(f.Runtime.Context);Assert.False(result.Available);return false;});
        var evaluated=outer.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(evaluated.Available);Assert.False(evaluated.Runnable);
    }
    [Fact]
    public void ConnectedLocatedCubeProgressSurvivesDeselectAndResumesPreviousWhenOutside()
    {
        using var f=new Fixture();
        int id=f.M.World.AddConnectedActiveObject(1,123,ObjectType.Block_LIGHTCUBE1);
        Assert.True(id>=0);f.Add((uint)id,20,22);
        var calls=new List<(uint,string,string?)>();
        f.Rig.Robot.Lights.Cubes.BehaviorStateCallObservedForTest=(cube,op,trigger,layer,modifier,lights,offset)=>calls.Add((cube,op,trigger));
        var gather=Spark(f).SparkedOwner!.Delegate!.GatherOwner!;
        gather.Selected();f.M.Whiteboard.AddBeacon(f.Robot.GetCheckedMainPose(),75f);
        gather.Update();gather.Update();
        Assert.Single(calls);Assert.Equal(((uint)id,"Play","GatherCubesCubeInBeacon"),calls[0]);
        gather.Deselected();gather.Selected();
        f.M.Whiteboard.AddBeacon(new Cozmo.Robot.Vision.Pose3d(Cozmo.Robot.Vision.Rotation3d.Identity,new Cozmo.Robot.Vision.Vec3(500,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),75f);
        gather.Update();Assert.Equal(((uint)id,"StopResume","GatherCubesCubeInBeacon"),calls[1]);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void GatherReportsObjectiveBeforeNeedsAndLatchesOnce(bool external)
    {
        using var f=new Fixture();var spark=Spark(f).SparkedOwner!;var gather=spark.Delegate!.GatherOwner!;
        var types=new[]{ObjectType.Block_LIGHTCUBE1,ObjectType.Block_LIGHTCUBE2,ObjectType.Block_LIGHTCUBE3};
        for(int i=0;i<3;i++)
        {
            int id=f.M.World.AddConnectedActiveObject(i+1,(uint)(100+i),types[i]);Assert.True(id>=0);
            var obj=new ObservableObject((uint)id,types[i],CubeGeometry.MarkersFor(types[i]))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(10+i*10,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
            f.M.World.AddLocatedObject(obj);
        }
        gather.Selected();f.M.Whiteboard.AddBeacon(f.Robot.GetCheckedMainPose(),75f);
        f.Robot.HasExternalInterface=external;
        var order=new List<string>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value&&value==11)order.Add("objective");});
        f.Rig.Engine.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed GatherCubes:"))order.Add("needs");};
        // Supplied gameplay state; actual Spark update checks its own end before delegate Update.
        typeof(NativeSparkedActivity).GetProperty("State",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(spark,2);
        spark.Update();
        Assert.Equal(2,spark.State);Assert.Equal(external?1u:0u,spark.Count);Assert.True(gather.Completed);
        spark.Update();Assert.Equal(external?3:2,spark.State);
        Assert.Equal(external?new[]{"objective","needs"}:new[]{"needs"},order);
        gather.Deselected();Assert.False(gather.Completed);Assert.Null(f.M.Whiteboard.GetActiveBeacon());
        f.Robot.HasExternalInterface=true;
    }
    [Fact]
    public void PriorityUsesEachCandidatesRunningByteBeforeReadiness()
    {
        using var f=new Fixture();
        var earlier=new Probe("earlier",()=>false);var running=new Probe("running",()=>throw new Exception("running must not be evaluated"));
        typeof(SteppedBehavior).GetField("_engineRunning",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(running,true);
        var chooser=new StrictPriorityChooser(new[]{"earlier","running"},new Dictionary<string,IBehavior>{{"earlier",earlier},{"running",running}}){StopOnUnavailable=true};
        var result=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);
        Assert.Same(running,result.Behavior);Assert.False(result.InputUnavailable);Assert.Equal(1,earlier.Calls);Assert.Equal(0,running.Calls);
    }
    [Fact]
    public void CubeLightCompletionRunsOutsidePrivateGateAndPreservesReentrantReplacement()
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);
        var lights=f.Rig.Robot.Lights.Cubes;
        var gate=typeof(CubeLightComponent).GetField("_gate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(lights)!;
        bool entered=false;int callbacks=0;
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",1,completed:()=>
        {
            callbacks++;
            entered=Task.Run(()=>{bool held=Monitor.TryEnter(gate,1000);if(held)Monitor.Exit(gate);return held;}).GetAwaiter().GetResult();
            Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        }));
        lights.StopLightAnim("Flash",1,ObjectType.Block_LIGHTCUBE1,pickDefaultLayer:false);
        Assert.True(entered);Assert.Equal(1,callbacks);
        Assert.NotNull(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1));
    }
    [Fact]
    public void DeselectionReentryReleasesOnlyOldDrivingAndIdleAcquisitions()
    {
        using var f=new Fixture();var activity=Spark(f);var manager=f.Robot.BehaviorManagerOwner!;
        manager.SetNativeRequestedSpark(38,0);activity.OnSelected(0);
        bool replaced=false;
        manager.Log+=entry=>
        {
            if(replaced || !entry.StartsWith("BehaviorManager.RemoveDisableReactionsLock:"))return;
            replaced=true;activity.OnSelected(0);
        };
        activity.OnDeselected(0);
        Assert.True(replaced);
        Assert.Equal(2,f.Rig.Robot.Animations.DrivingAnimations.Entries.Count(e=>e.LockName==activity.Id));
        Assert.Single(f.Rig.Robot.Animations.Scheduler.IdleStack.Where(e=>e.Lock=="Activity_"+activity.Id));
        activity.OnDeselected(0);
    }
    [Fact]
    public void ConfiguredAliasesUseTheirDifferentCooldownsWithSameCarriedTarget()
    {
        using var f=new Fixture();f.Add(7,200,22);f.Robot.CarryingOwner!.SetCarrying(7);
        var beacon=f.M.Whiteboard.AddBeacon(f.Robot.GetCheckedMainPose(),75f);
        beacon.FailedToFindLocation(f.Rig.Engine.Timer.SecondsF-10f);
        var hiking=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        var sparks=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["SparksBringCubeToBeacon"]);
        Assert.False(hiking.IsRunnable(f.Runtime.Context));Assert.True(sparks.IsRunnable(f.Runtime.Context));
    }
    [Fact]
    public void CanonicalThinkCreatesBeaconConsumedByCanonicalBring()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);f.Add(7,200,22);
        var think=Assert.IsType<ThinkAboutBeaconsBehavior>(f.Runtime.Bound["SparksThinkAboutBeacons"]);
        var bring=Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["SparksBringCubeToBeacon"]);
        Assert.NotNull(f.Rig.Robot.Animations.Library);Assert.NotNull(think.NativeBaseInputs);
        Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);Assert.False(f.Rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal(OffTreadsState.OnTreads,f.Rig.Robot.Sensors.OffTreadsState);
        Assert.Null(f.M.Whiteboard.GetActiveBeacon());
        var readiness=think.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(readiness.Available,"MISSING: canonical Think required host inputs unavailable");
        Assert.True(readiness.Runnable,"Canonical Think source gates returned false despite supplied fixture inputs");
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(think.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Equal(75d,f.M.Whiteboard.GetActiveBeacon()!.RadiusMm);
        f.Robot.CarryingOwner!.SetCarrying(7);
        Assert.True(bring.IsRunnable(f.Runtime.Context));
    }
    [Fact]
    public void RuntimeReentrantReplacementKeepsOnlyNewSelectionResources()
    {
        using var f=new Fixture();var manager=f.Robot.BehaviorManagerOwner!;
        manager.SetNativeRequestedSpark(38,0);f.Runtime.AdmitActivity("SparksGatherCubes");
        bool replaced=false;
        manager.Log+=line=>
        {
            if(replaced || !line.StartsWith("BehaviorManager.RemoveDisableReactionsLock:"))return;
            replaced=true;f.Runtime.AdmitActivity("SparksGatherCubes");
        };
        f.Runtime.AdmitActivity("SparksGatherCubes");
        Assert.True(replaced);
        Assert.Equal(2,f.Rig.Robot.Animations.DrivingAnimations.Entries.Count(e=>e.LockName=="SparksGatherCubes"));
        Assert.Single(f.Rig.Robot.Animations.Scheduler.IdleStack.Where(e=>e.Lock=="Activity_SparksGatherCubes"));
        Assert.Equal("SparksGatherCubes",f.Runtime.Observe().Activity);
    }
    [Fact]
    public void CanonicalThinkMissingLibraryIsUnavailableAtScopedChooser()
    {
        using var f=new Fixture();var think=Assert.IsType<ThinkAboutBeaconsBehavior>(f.Runtime.Bound["SparksThinkAboutBeacons"]);
        var library=typeof(Cozmo.Robot.Animation.CozmoAnimations).GetProperty("Library")!;
        var prior=library.GetValue(f.Rig.Robot.Animations);library.SetValue(f.Rig.Robot.Animations,null);
        try
        {
            var chooser=new StrictPriorityChooser(new[]{think.Id,"SparksBringCubeToBeacon"},f.Runtime.Bound){StopOnUnavailable=true};
            var decision=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);
            Assert.True(decision.InputUnavailable);Assert.Null(decision.Behavior);
        }
        finally {library.SetValue(f.Rig.Robot.Animations,prior);}
    }
    [Fact]
    public void StoppedLoopDiagnosticReentryDoesNotMutateReplacement()
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var lights=f.Rig.Robot.Lights.Cubes;
        Define(lights.Animations,"visible",Pattern("nonzero-visible",1000));
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",1));
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
        var infos=(System.Collections.IDictionary)typeof(CubeLightComponent).GetField("_infos",flags)!.GetValue(lights)!;
        var info=infos[ObjectType.Block_LIGHTCUBE1]!;var layers=(Array)info.GetType().GetField("Layers",flags)!.GetValue(info)!;
        var stack=(System.Collections.IList)layers.GetValue(1)!;
        Assert.Equal(2,stack.Count);stack[0]!.GetType().GetField("Stop",flags)!.SetValue(stack[0],true);
        bool current=true,inside=false,entered=false;
        var gate=typeof(CubeLightComponent).GetField("_gate",flags)!.GetValue(lights)!;
        // Unit seam at the diagnostic continuation; public LogLine is deferred by the pose gate.
        lights.StoppedLoopObservedForTest=()=>
        {
            if(inside)return;
            inside=true;entered=Task.Run(()=>{bool held=Monitor.TryEnter(gate,1000);if(held)Monitor.Exit(gate);return held;}).GetAwaiter().GetResult();
            current=false;Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",1));
        };
        lights.StopBehaviorStateLight(f.M.World.ConnectedObjects.Single().ObjectId,37,false,()=>current); // Visible trigger 37
        Assert.True(inside);Assert.True(entered);Assert.NotNull(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanceledTransitionRestoresPriorStateWithoutOldLedSend(bool replacement)
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var lights=f.Rig.Robot.Lights.Cubes;
        var cube=f.M.World.ConnectedObjects.Single().ObjectId;
        bool current=true,transition=false;
        typeof(CubeLightComponent).GetField("_transitionMessagesEnabled",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(lights,true);
        f.Rig.Engine.LogLine+=line=>
        {
            if(transition || !line.Contains("SendTransitionMessage 0x00638758"))return;
            transition=true;current=false;
            if(replacement)Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        };
        int mark=f.Rig.Mark();
        Assert.False(lights.PlayBehaviorStateLight(cube,17,false,new CubeLightModifier(new LedPattern[4],0,0,default),()=>current));
        Assert.True(transition);
        if(replacement)Assert.Equal("visible",lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value.Name);
        else Assert.Null(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1));
        Assert.Equal(replacement?1:0,f.Rig.SentSince(mark).Count(m=>m is Cozmo.Protocol.CubeLights));
    }
    [Fact]
    public void ActualDelegateReachedUnboundTailRetiresWithoutNativeCompletion()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        // Supplied native UnlockId name input for the checked picker-success leaf.
        var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=id=>id switch { 38=>"SparksGatherCubes",0x55=>"Count",_=>null };
        manager.SetNativeRequestedSpark(38,1);
        f.Runtime.AdmitActivity("SparksGatherCubes");manager.SwitchToNativeRequestedSpark();
        Assert.Equal(38,manager.RequestedSparkId);Assert.Equal(38,manager.ActiveSparkId);
        Assert.Equal((byte)1,manager.RequestedSparkSoft);Assert.Equal((byte)1,manager.ActiveSparkSoft);
        Assert.Equal((byte)0,manager.SparkCancelLatch);Assert.Null(manager.CurrentReactionTrigger);
        f.M.Whiteboard.AddBeacon(f.Robot.GetCheckedMainPose(),75f); // supplied existing beacon makes Think false
        var spark=Spark(f).SparkedOwner!;
        typeof(NativeSparkedActivity).GetProperty("State",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(spark,2);
        var reports=new List<bool>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x84,true,p=>{if(p is bool value)reports.Add(value);});
        manager.Update(0,0);
        Assert.True(spark.InputUnavailable);Assert.Null(manager.Activity);Assert.Null(f.Runtime.Observe().Activity);
        Assert.Empty(reports);Assert.Equal(0u,spark.Count);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualSecondGateMissingLibraryRetiresInsteadOfStartingThink(bool replace)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        // Supplied native UnlockId name input for the checked picker-success leaf.
        var manager=f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput=id=>id switch { 38=>"SparksGatherCubes",0x55=>"Count",_=>null };
        manager.SetNativeRequestedSpark(38,1);
        f.Runtime.AdmitActivity("SparksGatherCubes");manager.SwitchToNativeRequestedSpark();
        Assert.Equal(38,manager.RequestedSparkId);Assert.Equal(38,manager.ActiveSparkId);
        Assert.Equal((byte)1,manager.RequestedSparkSoft);Assert.Equal((byte)1,manager.ActiveSparkSoft);
        Assert.Equal((byte)0,manager.SparkCancelLatch);Assert.Null(manager.CurrentReactionTrigger);
        var spark=Spark(f).SparkedOwner!;
        typeof(NativeSparkedActivity).GetProperty("State",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(spark,2);
        var think=Assert.IsType<ThinkAboutBeaconsBehavior>(f.Runtime.Bound["SparksThinkAboutBeacons"]);
        var library=typeof(Cozmo.Robot.Animation.CozmoAnimations).GetProperty("Library")!;var saved=library.GetValue(f.Rig.Robot.Animations);
        int calls=0;think.RequiredProcessId=1;think.RequiredProcessRunning=()=>{calls++;library.SetValue(f.Rig.Robot.Animations,null);return true;};
        bool replaced=false;
        SteppedBehavior.ReadinessUnavailableObservedForTest=()=>
        {
            if(!replace || replaced)return;replaced=true;library.SetValue(f.Rig.Robot.Animations,saved);
            f.Runtime.AdmitActivity("SparksGatherCubes");
        };
        try
        {
            manager.Update(0,0);
            Assert.Equal(1,calls);Assert.Equal(replace,replaced);
            if(replace){Assert.NotNull(manager.Activity);Assert.Equal("SparksGatherCubes",f.Runtime.Observe().Activity);}
            else {Assert.Null(manager.Activity);Assert.Null(f.Runtime.Observe().Activity);}
            Assert.Null(manager.Current);Assert.Null(f.M.Whiteboard.GetActiveBeacon());
        }
        finally {SteppedBehavior.ReadinessUnavailableObservedForTest=null;library.SetValue(f.Rig.Robot.Animations,saved);}
    }
    private static string Pattern(string name,uint duration) => $$"""
        {"pattern":{"onColors":[[11,2,3,255],[11,2,3,255],[11,2,3,255],[11,2,3,255]],
        "offColors":[[1,1,1,255],[1,1,1,255],[1,1,1,255],[1,1,1,255]],
        "onPeriod_ms":[1,1,1,1],"offPeriod_ms":[2,2,2,2],"transitionOnPeriod_ms":[0,0,0,0],
        "transitionOffPeriod_ms":[0,0,0,0],"offset":[0,0,0,0],"rotationPeriod_ms":0},
        "duration_ms":{{duration}},"canBeOverridden":true,"patternDebugName":"{{name}}"}
        """;
    private static void Define(CubeLightAnimations animations,string name,params string[] patterns)
    {
        Assert.True(Json.TryParseFirst(System.Text.Encoding.UTF8.GetBytes("{\""+name+"\":["+string.Join(",",patterns)+"]}"),out var doc));
        using(doc)animations.Define(doc.RootElement,null);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanceledZeroDurationPlayRestoresDisplacedLowerEntryUnderReplacement(bool replacementClears)
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var lights=f.Rig.Robot.Lights.Cubes;
        Define(lights.Animations,"sleep",Pattern("prior",1000));Define(lights.Animations,"flash",Pattern("old",0));Define(lights.Animations,"visible",Pattern("replacement",replacementClears?0u:1000u));
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",1));
        var cube=f.M.World.ConnectedObjects.Single().ObjectId;bool current=true,replaced=false;
        typeof(CubeLightComponent).GetField("_transitionMessagesEnabled",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(lights,true);
        f.Rig.Engine.LogLine+=line=>
        {
            if(replaced || !line.Contains("SendTransitionMessage 0x00638758"))return;
            replaced=true;current=false;Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        };
        int mark=f.Rig.Mark();
        Assert.False(lights.PlayBehaviorStateLight(cube,15,false,new CubeLightModifier(new LedPattern[4],0,0,default),()=>current));
        Assert.True(replaced);Assert.Equal(replacementClears?1:2,lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value.Count);
        Assert.Single(f.Rig.SentSince(mark).OfType<CubeLights>());
        lights.StopLightAnim("Visible",1,ObjectType.Block_LIGHTCUBE1,pickDefaultLayer:false);
        if(replacementClears)Assert.Null(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1));
        else Assert.Equal("sleep",lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value.Name);
    }
    [Fact]
    public void CanceledBlendRestoresOwnedSlotAndDanglingWithoutOverwritingReplacement()
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var lights=f.Rig.Robot.Lights.Cubes;
        Define(lights.Animations,"sleep",Pattern("first",1000),Pattern("second",1000));Define(lights.Animations,"flash",Pattern("old",1000));Define(lights.Animations,"visible",Pattern("replacement",1000));
        var zero=new CubeLightModifier(new LedPattern[4],0,0,default);
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",1,modifier:true,lights:zero));
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
        var infos=(System.Collections.IDictionary)typeof(CubeLightComponent).GetField("_infos",flags)!.GetValue(lights)!;
        var info=infos[ObjectType.Block_LIGHTCUBE1]!;var layers=(Array)info.GetType().GetField("Layers",flags)!.GetValue(info)!;
        var stack=(System.Collections.IList)layers.GetValue(1)!;var lower=stack[0]!;lower.GetType().GetField("Index",flags)!.SetValue(lower,1);
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        var cube=f.M.World.ConnectedObjects.Single().ObjectId;bool current=true,replaced=false;
        typeof(CubeLightComponent).GetField("_transitionMessagesEnabled",flags)!.SetValue(lights,true);
        f.Rig.Engine.LogLine+=line=>
        {
            if(replaced || !line.Contains("SendTransitionMessage 0x00638758"))return;
            replaced=true;current=false;Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        };
        int mark=f.Rig.Mark();Assert.False(lights.PlayBehaviorStateLight(cube,15,true,zero,()=>current));
        Assert.True(replaced);Assert.False((bool)lower.GetType().GetField("Dangling",flags)!.GetValue(lower)!);
        Assert.Equal(3,lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value.Count);
        Assert.Single(f.Rig.SentSince(mark).OfType<CubeLights>());
        var patterns=(CubeLightPattern[])lower.GetType().GetProperty("Anim",flags)!.GetValue(lower)!;
        Assert.Equal(new[]{"first","second"},patterns.Select(p=>p.DebugName));
        Assert.Equal(1,(int)info.GetType().GetField("CurrentLayer",flags)!.GetValue(info)!);
    }
    // M1-034/M4-045: old shortening is canceled after a replacement writes
    // the same short slot, a valid long slot, or clears the buried entry.
    [Theory]
    [InlineData("short")] [InlineData("long")] [InlineData("clear")]
    public void CanceledShortBlendRespectsFinalReplacementSlot(string mode)
    {
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var lights=f.Rig.Robot.Lights.Cubes;
        Define(lights.Animations,"sleep",Pattern("first",1000),Pattern("second",1000));
        Define(lights.Animations,"flash",Pattern("short",1000));Define(lights.Animations,"visible",Pattern("middle",1000));
        var zero=new CubeLightModifier(new LedPattern[4],0,0,default);
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",1,modifier:true,lights:zero));
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
        var infos=(System.Collections.IDictionary)typeof(CubeLightComponent).GetField("_infos",flags)!.GetValue(lights)!;
        var info=infos[ObjectType.Block_LIGHTCUBE1]!;var layers=(Array)info.GetType().GetField("Layers",flags)!.GetValue(info)!;
        var stack=(System.Collections.IList)layers.GetValue(1)!;var lower=stack[0]!;lower.GetType().GetField("Index",flags)!.SetValue(lower,1);
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
        var cube=f.M.World.ConnectedObjects.Single().ObjectId;bool current=true,replaced=false;var logs=new List<string>();
        typeof(CubeLightComponent).GetField("_transitionMessagesEnabled",flags)!.SetValue(lights,true);
        f.Rig.Engine.LogLine+=line=>
        {
            logs.Add(line);
            if(replaced || !line.Contains("SendTransitionMessage 0x00638758"))return;
            replaced=true;current=false;
            if(mode=="clear")Define(lights.Animations,"visible",Pattern("clear",0));
            Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Visible",1));
            if(mode=="long")Define(lights.Animations,"flash",Pattern("long0",1000),Pattern("long1",1000));
            Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",1,modifier:true,lights:zero));
        };
        Assert.False(lights.PlayBehaviorStateLight(cube,15,true,zero,()=>current));Assert.True(replaced);
        if(mode=="clear")Assert.DoesNotContain(stack.Cast<object>(),entry=>ReferenceEquals(entry,lower));
        else Assert.Equal(mode=="short",(bool)lower.GetType().GetField("Dangling",flags)!.GetValue(lower)!);
        lights.StopLightAnim("Flash",1,ObjectType.Block_LIGHTCUBE1,pickDefaultLayer:false);
        lights.StopLightAnim("Visible",1,ObjectType.Block_LIGHTCUBE1,pickDefaultLayer:false);
        if(mode=="short")Assert.Contains(logs,line=>line.Contains("M1-034 / M4-045"));
        if(mode=="long")Assert.Equal("sleep",lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value.Name);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RobotRemovalReleasesResourcesThroughOriginalOwners(bool admitted)
    {
        var f=new Fixture();var activity=Spark(f);var manager=f.Robot.BehaviorManagerOwner!;
        var driving=f.Rig.Robot.Animations.DrivingAnimations;var scheduler=f.Rig.Robot.Animations.Scheduler;
        manager.SetNativeRequestedSpark(38,0);
        if(admitted)f.Runtime.AdmitActivity("SparksGatherCubes");else activity.OnSelected(0);
        Assert.Equal(2,driving.Entries.Count(e=>e.LockName==activity.Id));
        Assert.Single(scheduler.IdleStack.Where(e=>e.Lock=="Activity_"+activity.Id));
        // Actual robot lifetime clears BehaviorManagerOwner before runtime retirement.
        f.Rig.Dispose();
        Assert.DoesNotContain(driving.Entries,e=>e.LockName==activity.Id);
        Assert.DoesNotContain(scheduler.IdleStack,e=>e.Lock=="Activity_"+activity.Id);
    }
}


