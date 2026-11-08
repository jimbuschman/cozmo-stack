using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeChooserDispatchTests
{
    private static string Resources()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {var path=Path.Combine(d.FullName,"re-analysis","obb","assets","cozmo_resources");if(File.Exists(Path.Combine(path,"config","engine","behaviorSystem","activities_config.json")))return path;}
        throw new DirectoryNotFoundException("shipped resources");
    }
    private static SelectionChooser Selection(PickupPyramidNativeExecutionTests.Fixture f)=>
        Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a=>a.Id=="Selection").Chooser);

    [Fact]
    public void BootAndQueuedIdAndExecutableUseTheSameRealWait()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var manager=f.Robot.BehaviorManagerOwner!;
        var selection=Selection(f);var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        Assert.NotNull(manager.Activity);Assert.Same(wait,selection.Wait);
        Assert.Equal(1,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x94,false));
        Assert.Equal(1,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x93,false));
        for(int i=0;i<10&&!wait.EngineRunning;i++)f.Rig.Tick();
        Assert.Same(wait,manager.Current);Assert.True(wait.EngineRunning);Assert.Null(wait.OwnedRunner);
        f.Rig.Robot.ExecuteBehaviorById(0xB2,2);f.Rig.Tick();
        Assert.Same(wait,selection.Requested);Assert.Equal(2,selection.NumRuns);
        f.Rig.Robot.ExecuteBehaviorByExecutableType(11,-1);f.Rig.Tick();
        Assert.Same(wait,selection.Requested);Assert.Equal(-1,selection.NumRuns);
        int handlerCount=-1;f.Rig.Robot.ExecuteBehaviorByExecutableType(255,7);f.Rig.Engine.PostGameMessage(()=>handlerCount=selection.NumRuns);f.Rig.Tick();
        Assert.Null(selection.Requested);Assert.Equal(7,handlerCount);Assert.Equal(6,selection.NumRuns);
    }
    [Fact]
    public void DirectStartWithoutSelectionRequestIsReplacedByActualWaitOnNextTick()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var manager=f.Robot.BehaviorManagerOwner!;
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var look=Assert.IsType<NativeLookForFaceAndCubeBehavior>(f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"]);
        Assert.True(manager.StartAsync(look.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var old=Assert.IsAssignableFrom<ActionRunner>(look.OwnedRunner);f.Rig.Tick();
        Assert.Same(f.Runtime.Bound["Wait"],manager.Current);Assert.True(old.DestructionObserved);Assert.False(look.EngineRunning);
    }
    [Fact]
    public void ExecutableNameTableUsesNativeCountAndUnknownFallback()
    {
        string[] names={"EnrollFace","FindFaces","FireTruckAlarm","FistBump","KnockOverCubes","LiftLoadTest","LookAroundInPlace","PounceOnMotion","RollBlock","StackBlocks","TrackLaser","Wait","Count"};
        for(byte i=0;i<names.Length;i++)
        {using var json=JsonDocument.Parse("{\"executableBehaviorType\":\""+names[i]+"\"}");Assert.Equal(i,ExecutableBehaviorNames.FromConfig(json.RootElement));}
        using var missing=JsonDocument.Parse("{}");Assert.Equal(12,ExecutableBehaviorNames.FromConfig(missing.RootElement));
        using var unknown=JsonDocument.Parse("{\"executableBehaviorType\":\"unknown\"}");Assert.Equal(0,ExecutableBehaviorNames.FromConfig(unknown.RootElement));
    }
    [Fact]
    public void NoExternalInterfaceConstructsWaitButNoSelectionSubscriptions()
    {
        using var rig=new M4ControlTests.Rig(Resources());
        rig.Engine.BeforeDerivedConstructionForTest=robot=>robot.HasExternalInterface=false;
        rig.ToSynced();var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        var chooser=Assert.IsType<SelectionChooser>(runtime.Tree.Single(a=>a.Id=="Selection").Chooser);
        Assert.Same(runtime.Bound["Wait"],chooser.Wait);
        Assert.Equal(0,rig.Engine.AnimationMessages.SubscriberCountForTest(0x94,false));
        Assert.Equal(0,rig.Engine.AnimationMessages.SubscriberCountForTest(0x93,false));
    }
    [Fact]
    public void UnbuiltExecutableOwnerIsUnavailableWithoutSkippingOrReplacingPriorRequest()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var chooser=Selection(f);
        f.Rig.Robot.ExecuteBehaviorById(0xB2,-1);f.Rig.Tick();Assert.Same(f.Runtime.Bound["Wait"],chooser.Requested);
        var missing=new List<string>();void Observe(string text)=>missing.Add(text);
        SteppedBehavior.MissingReported+=Observe;
        try {f.Rig.Robot.ExecuteBehaviorByExecutableType(5,3);f.Rig.Tick();
            Assert.Same(f.Runtime.Bound["Wait"],chooser.Requested);Assert.Equal(-1,chooser.NumRuns);
            Assert.Contains(missing,s=>s.Contains("configured executable owner LiftLoadTest is unbuilt"));}
        finally {SteppedBehavior.MissingReported-=Observe;}
    }
    [Fact]
    public void ConfiguredUnbuiltIdIsUnavailableWhileUnknownIdClearsRequest()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var chooser=Selection(f);
        f.Rig.Robot.ExecuteBehaviorById(0xB2,-1);f.Rig.Tick();Assert.Same(f.Runtime.Bound["Wait"],chooser.Requested);
        var missing=new List<string>();void Observe(string text)=>missing.Add(text);
        SteppedBehavior.MissingReported+=Observe;
        try
        {
            f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.LiftLoadTest,3);f.Rig.Tick();
            Assert.Same(f.Runtime.Bound["Wait"],chooser.Requested);Assert.Equal(-1,chooser.NumRuns);
            Assert.Contains(missing,s=>s.Contains("configured behavior owner LiftLoadTest is unbuilt"));
            int handlerCount=-1;f.Rig.Robot.ExecuteBehaviorById(255,7);f.Rig.Engine.PostGameMessage(()=>handlerCount=chooser.NumRuns);f.Rig.Tick();
            Assert.Null(chooser.Requested);Assert.Equal(7,handlerCount);Assert.Equal(6,chooser.NumRuns);
        }
        finally {SteppedBehavior.MissingReported-=Observe;}
    }
    [Fact]
    public void RetiredChooserUnsubscribesAndIgnoresQueuedPayload()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var selection=Selection(f);
        f.Rig.Robot.ExecuteBehaviorById(0xB2,2);f.Runtime.Dispose();
        f.Rig.Engine.AnimationMessages.Deliver(0x94,false,new byte[]{0xB2,2,0,0,0});
        Assert.Null(selection.Requested);Assert.Equal(0,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x94,false));
        Assert.Equal(0,f.Rig.Engine.AnimationMessages.SubscriberCountForTest(0x93,false));
    }
    [Fact]
    public void QueuedRequestsRetainSelectionWhileInactiveAndProcessHandoffFollowsLifecycle()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var selection=Selection(f);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.Hiking_VisitInterestingEdge,3);f.Rig.Tick();
        Assert.Same(f.Runtime.Bound["Hiking_VisitInterestingEdge"],selection.Requested);
        Assert.True(f.Runtime.AI.Analyzer.IsProcessRunning(1));
        f.Rig.Robot.ActivateHighLevelActivity("Freeplay");f.Rig.Tick();
        Assert.False(f.Runtime.AI.Analyzer.IsProcessRunning(1));
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.Wait,4);f.Rig.Tick();
        Assert.Same(f.Runtime.Bound["Wait"],selection.Requested);Assert.Equal(4,selection.NumRuns);
        f.Rig.Robot.ActivateHighLevelActivity("Selection");f.Rig.Tick();
        Assert.Same(f.Runtime.Bound["Wait"],selection.Requested);Assert.NotNull(f.Robot.BehaviorManagerOwner!.Activity);
    }
    [Fact]
    public void RepeatedSelectionRunsAdapterTailAfterPublicQueuedSparkRequest()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var observer=FreeplayStack.CreateNative(f.Rig.Robot);
        var manager=f.Robot.BehaviorManagerOwner!;var selection=manager.Activity;
        observer.ActivateSpark(29);f.Rig.Tick();Assert.Equal(29,manager.RequestedSparkId);
        byte requestedSoft=manager.RequestedSparkSoft,activeSoft=manager.ActiveSparkSoft;
        f.Rig.Robot.ActivateHighLevelActivity("Selection");f.Rig.Tick();
        Assert.Same(selection,manager.Activity);Assert.Equal(0x55,manager.RequestedSparkId);Assert.Equal(0x55,manager.ActiveSparkId);
        Assert.Equal(requestedSoft,manager.RequestedSparkSoft);Assert.Equal(activeSoft,manager.ActiveSparkSoft);
    }
    [Theory]
    [InlineData("Feeding", false)]
    [InlineData("MeetCozmoFindFaces", true)]
    public void NativeHighLevelRequestStillRunsCapturedAdapterTail(string requested, bool built)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();using var observer=FreeplayStack.CreateNative(f.Rig.Robot);
        var manager=f.Robot.BehaviorManagerOwner!;var before=manager.Activity;
        observer.ActivateSpark(29);f.Rig.Tick();Assert.Equal(29,manager.RequestedSparkId);
        float parent=manager.ParentSwitchTime,freeplay=manager.FreeplayStartTime;
        byte activeSoft=manager.ActiveSparkSoft,requestedSoft=manager.RequestedSparkSoft,cancel=manager.SparkCancelLatch;
        f.Rig.Robot.ActivateHighLevelActivity(requested);f.Rig.Tick();
        if (built)
        {
            Assert.NotSame(before, manager.Activity);
            Assert.Equal((byte)6, f.Robot.BehaviorPublicState.State.Activity);
            Assert.True(manager.ParentSwitchTime > parent);
        }
        else
        {
            Assert.Same(before, manager.Activity);
            Assert.Equal(parent, manager.ParentSwitchTime);
        }
        Assert.Equal(0x55,manager.ActiveSparkId);Assert.Equal(0x55,manager.RequestedSparkId);
        Assert.Equal(freeplay,manager.FreeplayStartTime);
        Assert.Equal(activeSoft,manager.ActiveSparkSoft);Assert.Equal(requestedSoft,manager.RequestedSparkSoft);Assert.Equal(cancel,manager.SparkCancelLatch);
    }
    [Fact]
    public void HostSubactivityDoesNotRunNativeHighLevelAdapterTail()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();using var observer=FreeplayStack.CreateNative(f.Rig.Robot);
        var manager=f.Robot.BehaviorManagerOwner!;observer.ActivateSpark(29);f.Rig.Tick();
        f.Rig.Robot.ActivateHighLevelActivity("PutDownDispatch");f.Rig.Tick();
        Assert.Equal(29,manager.RequestedSparkId);
    }
    [Fact]
    public void QueuedOldOwnerRequestCannotClearReplacementSpark()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.ActivateHighLevelActivity("Feeding");
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        using var observer=FreeplayStack.CreateNative(f.Rig.Robot);observer.ActivateSpark(29);f.Rig.Tick();
        Assert.Equal(29,replacement.BehaviorManagerOwner!.RequestedSparkId);
    }
    [Fact]
    public void NestedHighLevelCallbackStillUsesOuterCapturedAdapterInput()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();using var observer=FreeplayStack.CreateNative(f.Rig.Robot);
        var manager=f.Robot.BehaviorManagerOwner!;observer.ActivateSpark(29);f.Rig.Tick();bool nested=false;
        void OnMissing(string text)
        {
            if(nested || !text.Contains("native high-level activity Feeding"))return;
            nested=true;f.Runtime.AdmitNativeHighLevel("Freeplay"); // synchronous source-log callback reentry
        }
        SteppedBehavior.MissingReported+=OnMissing;
        try {f.Rig.Robot.ActivateHighLevelActivity("Feeding");f.Rig.Tick();}
        finally {SteppedBehavior.MissingReported-=OnMissing;}
        Assert.True(nested);Assert.Same(f.Runtime.FreeplayObserver,manager.Activity);
        Assert.Equal(0x55,manager.ActiveSparkId);Assert.Equal(0x55,manager.RequestedSparkId);
    }
    [Fact]
    public void NativeSelectionCountUsesOwnRunningAndZeroPreservesLatch()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var chooser=Selection(f);
        var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        f.Rig.Robot.ExecuteBehaviorById(0xB2,2);f.Rig.Tick();
        Assert.True(wait.EngineRunning);chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);
        Assert.True(chooser.RunningLatch);Assert.Equal(2,chooser.NumRuns);
        f.Rig.Robot.ExecuteBehaviorById(0xB2,0);f.Rig.Tick();Assert.True(chooser.RunningLatch);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);Assert.True(chooser.RunningLatch);
        f.Rig.Robot.ExecuteBehaviorById(255,2);f.Rig.Tick();
        Assert.Null(chooser.Requested);Assert.Equal(1,chooser.NumRuns);Assert.False(chooser.RunningLatch);
    }
    [Fact]
    public void ConfiguredScoringReloadMutatesSharedOwnerBeforeDuplicateDedup()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        using var first=JsonDocument.Parse("{\"type\":\"Scoring\",\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":3,\"repetitionPenalty\":{\"nodes\":[{\"x\":0,\"y\":0.5}]},\"runningPenalty\":{\"nodes\":[{\"x\":0,\"y\":0.5}]}}}]}");
        using var second=JsonDocument.Parse("{\"type\":\"Scoring\",\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{}},{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":7}}]}");
        var a=Assert.IsType<ScoringChooser>(ActivityTreeLoader.BuildChooser(first.RootElement,f.Runtime.Bound,null,null,f.Robot));
        Assert.Equal(3,wait.NativeScoring!.FlatScore);
        using var reset=JsonDocument.Parse("{\"type\":\"Scoring\",\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{}}]}");
        _=ActivityTreeLoader.BuildChooser(reset.RootElement,f.Runtime.Bound,null,null,f.Robot);
        Assert.Equal(3,wait.NativeScoring!.FlatScore);Assert.Null(wait.NativeScoring.RepetitionPenalty);Assert.Null(wait.NativeScoring.RunningPenalty);
        var b=Assert.IsType<ScoringChooser>(ActivityTreeLoader.BuildChooser(second.RootElement,f.Runtime.Bound,null,null,f.Robot));
        Assert.Equal(7,wait.NativeScoring!.FlatScore);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};a.ContextRandom=b.ContextRandom=f.Rig.Engine.ContextRandom;
        try {var da=a.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);Assert.Equal(7,Assert.Single(da.Scores).Score);Assert.Equal(2,words);
            words=0;var db=b.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);Assert.Equal(7,Assert.Single(db.Scores).Score);Assert.Equal(2,words);}
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void RunningScoreUsesOwnTimestampEvenWhenManagerCurrentArgumentDiffers()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
        for(int i=0;i<10&&!wait.EngineRunning;i++)f.Rig.Tick();Assert.True(wait.EngineRunning);
        using var config=JsonDocument.Parse("{\"type\":\"Scoring\",\"scoreBonusForCurrentBehavior\":{\"nodes\":[{\"x\":-3,\"y\":1},{\"x\":-1,\"y\":3}]},\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":7}}]}");
        var chooser=Assert.IsType<ScoringChooser>(ActivityTreeLoader.BuildChooser(config.RootElement,f.Runtime.Bound,null,null,f.Robot));
        float now=(float)wait.RunningPenaltyClockSec-2f;
        var decision=chooser.GetDesiredActiveBehavior(null,999,f.Runtime.Context,now);
        Assert.Same(wait,decision.Behavior);Assert.Equal((double)(7f+2f+BitConverter.UInt32BitsToSingle(0x3DCCCCCD)),Assert.Single(decision.Scores).Score);
    }
    [Fact]
    public void ConfiguredTreeLoadsNormalInterludeThenChildAgainstSameScoreOwner()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        string resources=Directory.CreateTempSubdirectory("chooser-order-").FullName;
        try
        {
            string dir=Path.Combine(resources,"config","engine","behaviorSystem");string activities=Path.Combine(dir,"activities");Directory.CreateDirectory(activities);
            File.WriteAllText(Path.Combine(dir,"activities_config.json"),"[{\"activityID\":\"Freeplay\",\"behaviorChooser\":{\"type\":\"Scoring\",\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":3}}]},\"interludeBehaviorChooser\":{\"type\":\"Scoring\",\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{}}]},\"subActivities\":[{\"activityID\":\"Child\"}]}]");
            File.WriteAllText(Path.Combine(activities,"child.json"),"{\"activityID\":\"Child\",\"behaviorChooser\":{\"type\":\"Scoring\",\"behaviors\":[{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":7}},{\"behaviorID\":\"Wait\",\"scoring\":{}}]}}");
            var tree=ActivityTreeLoader.LoadResources(resources,f.Runtime.Bound,new RepetitionPenalty(),f.Robot);
            var wait=Assert.IsType<NativeWaitBehavior>(f.Runtime.Bound["Wait"]);
            Assert.Equal(7,wait.NativeScoring!.FlatScore);Assert.Null(wait.NativeScoring.RepetitionPenalty);Assert.Null(wait.NativeScoring.RunningPenalty);
            Assert.IsType<ScoringChooser>(Assert.Single(tree).Chooser);Assert.IsType<ScoringChooser>(tree[0].InterludeChooser);
            Assert.IsType<ScoringChooser>(Assert.Single(tree[0].SubActivities).Chooser);
        }
        finally {Directory.Delete(resources,true);}
    }
    [Fact]
    public void ReentrantAnalyzerHandoffCannotOverwriteNewerSelection()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var chooser=Selection(f);
        var edge=f.Runtime.Bound["Hiking_VisitInterestingEdge"];var wait=f.Runtime.Bound["Wait"];
        chooser.RequestBehavior(edge,2);bool entered=false;
        chooser.SetAnalyzerEnabled=(behavior,key,enabled)=>
        {
            if(!entered && ReferenceEquals(behavior,edge) && !enabled)
            {entered=true;chooser.RequestBehavior(wait,9);}
        };
        chooser.RequestBehavior(null,4);
        Assert.True(entered);Assert.Same(wait,chooser.Requested);Assert.Equal(9,chooser.NumRuns);
    }
    [Fact]
    public void ReentrantLogSubscriberCannotOverwriteNewerRequest()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var chooser=Selection(f);var wait=f.Runtime.Bound["Wait"];bool entered=false;
        chooser.SourceLog=(_,_,_,_)=>{if(!entered){entered=true;chooser.RequestBehavior(wait,9);}}; // supplied synchronous sink reentry.
        chooser.HandleExecuteBehavior(new ExecuteBehaviorByIDMessage((byte)BehaviorID.Hiking_VisitInterestingEdge,4));
        Assert.True(entered);Assert.Same(wait,chooser.Requested);Assert.Equal(9,chooser.NumRuns);
    }
}
