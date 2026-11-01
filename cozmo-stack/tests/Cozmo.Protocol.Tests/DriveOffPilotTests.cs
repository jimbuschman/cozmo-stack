using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class DriveOffPilotTests
{
    private static string ShippedResources()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
        {
            string path=Path.Combine(directory.FullName,"re-analysis","obb","assets","cozmo_resources");
            if(File.Exists(Path.Combine(path,"config","engine","behaviorSystem","activities_config.json")))return path;
        }
        throw new InvalidOperationException("Required shipped pilot fixture assets are absent");
    }

    [Fact]
    public void CanonicalConfiguredNeedsActionsUseTheActualBehaviorMap()
    {
        using var rig=new M4ControlTests.Rig(ShippedResources());rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;var ids=Assert.IsAssignableFrom<IReadOnlyDictionary<string,string>>(runtime.Context.NeedsActionIds);
        var expected=new (string Behavior,string Action,int Raw)[] {
            ("CubeLiftWorkout","Workout",44),("FPPeekABoo","PeekAboo",29),("KnockOverCubes","KnockDownCubes",27),
            ("PopAWheelie","PopAWheelie",33),("PounceOnMotion_Socialize","Pounce",35),("RollBlockOnSide","RollACube",39),
            ("RollBlockOnSideLowScore","RollACube",39),("StackBlocks","StackCube",42),("Hiking_PounceOnMotion","Pounce",35),
            ("Hiking_RollCube","RollACube",39),("SparksCubeLiftWorkout","Workout_Sparked",45),("SparksFireTruckAlarm","FireTruckAlarm",84),
            ("SparksFistBump","FistBump_Sparked",19),("SparksKnockOverCubes","KnockDownCubes_Sparked",28),("SparksPeekABoo","PeekAboo_Sparked",30),
            ("SparksPickUpCube","PickupCube_Sparked",32),("SparksPopAWheelie","PopAWheelie_Sparked",34),("SparksPounceOnMotion","Pounce_Sparked",36),
            ("SparksRollBlock","RollACube_Sparked",40),("SparksStackBlock","StackCube_Sparked",43),("SparksTrackLaser","Laser_Sparked",49),
            ("FistBump","FistBump",18)};
        Assert.Equal(expected.Length,ids.Count);
        foreach(var row in expected){Assert.Equal(row.Action,ids[row.Behavior]);Assert.Equal(row.Raw,(int)Enum.Parse<NeedsActionId>(row.Action));}
        // Explicit helper boundary: actual configured owner/context and engine Needs recipient.
        var reports=new List<string>();rig.Engine.Needs.Log+=reports.Add;
        var behavior=runtime.Bound["SparksRollBlock"];
        Assert.Equal("PickupCube",BehaviorNeedsActions.Complete(behavior,runtime.Context,"PickupCube"));
        Assert.Equal("RollACube_Sparked",BehaviorNeedsActions.Complete(behavior,runtime.Context));
        Assert.Contains(reports,line=>line.StartsWith("needs.action_completed PickupCube:"));
        Assert.Contains(reports,line=>line.StartsWith("needs.action_completed RollACube_Sparked:"));
        Assert.Null(BehaviorNeedsActions.Complete(runtime.Bound["DriveOffCharger"],runtime.Context));
    }

    [Fact]
    public void CanonicalSelectionChooserUsesTheActualAnalyzer()
    {
        using var rig=new M4ControlTests.Rig(ShippedResources());rig.ToSynced();rig.State();
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        var selection=Assert.IsType<SelectionChooser>(runtime.Tree.Single(a=>a.Id=="Selection").Chooser);
        var edge=Assert.IsType<VisitInterestingEdgeBehavior>(runtime.Bound["Hiking_VisitInterestingEdge"]);
        Assert.Equal(1,edge.RequiredProcessId);Assert.False(runtime.AI.Analyzer.IsProcessRunning(1));
        selection.HandleExecuteBehavior(new ExecuteBehaviorByIDMessage((byte)BehaviorID.Hiking_VisitInterestingEdge,-1));
        Assert.True(runtime.AI.Analyzer.IsProcessRunning(1));
        selection.HandleExecuteBehavior(new ExecuteBehaviorByIDMessage((byte)BehaviorID.Wait,-1));
        Assert.False(runtime.AI.Analyzer.IsProcessRunning(1));
    }

    [Fact]
    public void CanonicalNeedsProjectionUsesTheContainerFirstConfigWinner()
    {
        string temporary=Directory.CreateTempSubdirectory("needs-config-winner-").FullName;
        try
        {
            string source=Path.Combine(ShippedResources(),"config","engine","behaviorSystem");
            string destination=Path.Combine(temporary,"config","engine","behaviorSystem");
            foreach(string path in Directory.EnumerateFiles(source,"*.json",SearchOption.AllDirectories))
            {
                string target=Path.Combine(destination,Path.GetRelativePath(source,path));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(path,target);
            }
            string original=Path.Combine(destination,"behaviors","freeplay","sparkable","sparksRollBlock.json");
            var json=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(original),
                documentOptions:new System.Text.Json.JsonDocumentOptions{CommentHandling=System.Text.Json.JsonCommentHandling.Skip,AllowTrailingCommas=true})!;
            json["needsActionID"]="PickupCube";
            File.WriteAllText(Path.Combine(destination,"behaviors","000-first.json"),json.ToJsonString());
            using var rig=new M4ControlTests.Rig(temporary);rig.ToSynced();rig.State();
            var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
            Assert.IsType<NativeRollBlockBehavior>(runtime.Bound["SparksRollBlock"]);
            Assert.Equal("PickupCube",runtime.Context.NeedsActionIds!["SparksRollBlock"]);
            Assert.Equal("PickupCube",rig.Engine.HostBehaviorMap![BehaviorID.SparksRollBlock].GetProperty("needsActionID").GetString());
        }
        finally{Directory.Delete(temporary,true);}
    }

    [Fact]
    public void CanonicalFreeplayPicksShippedPutDownAndStartsDriveOff()
    {
        // Queued host string semantic adapter -> 005A106C -> 005AE29C configured parent.
        using var rig=new M4ControlTests.Rig(ShippedResources());
        rig.Robot.Animations.LoadFrom(ShippedResources());
        rig.ToSynced();rig.Calibrate();
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp);
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var robot=rig.Engine.Robot!;var runtime=robot.NativeBehaviorRuntime!;
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,
            origin:robot.CurrentOriginId,frame:robot.PoseFrameId));rig.Tick(10);
        using var observer=FreeplayStack.CreateNative(rig.Robot);
        Assert.Same(runtime.Tree,observer.Tree);Assert.Same(runtime.Tracker,observer.DataTracker);
        var behavior=Assert.IsType<DriveOffChargerBehavior>(runtime.Bound["DriveOffCharger"]);
        int mark=rig.Mark();
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(10);
        Assert.Same(runtime.FreeplayObserver,robot.BehaviorManagerOwner!.Activity);
        Assert.Equal("PutDownDispatch",runtime.FreeplayObserver.Current?.Id);
        // Real ReturnedToTreads may preempt the immediate start; allow its source
        // wait and the normal chooser/resume path to run inside the 5 s window.
        for(int i=0;i<30 && !rig.SentSince(mark).OfType<AppendPathSegmentLine>().Any();i++)rig.Tick(50);
        Assert.Contains(rig.SentSince(mark),message=>message is AppendPathSegmentLine);
        Assert.Same(behavior,robot.BehaviorManagerOwner.Current);
        var before=robot.BehaviorManagerOwner.Current;
        _=observer.Tick(999,999000,rig.Robot,null,null);
        Assert.Same(before,robot.BehaviorManagerOwner.Current);
        float entry=runtime.HighLevelEntryTime,first=robot.BehaviorManagerOwner.FreeplayStartTime;
        var pauses=runtime.Tracker.PausedFlags.ToHashSet();
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(10);
        Assert.Same(before,robot.BehaviorManagerOwner.Current);
        Assert.Equal(entry,runtime.HighLevelEntryTime);Assert.Equal(first,robot.BehaviorManagerOwner.FreeplayStartTime);
        Assert.True(pauses.SetEquals(runtime.Tracker.PausedFlags));
        // The public queued Spark input is consumed by the same manager and parent.
        observer.ActivateSpark(9);rig.Tick(10);
        Assert.Equal(9,robot.BehaviorManagerOwner.RequestedSparkId);
        Assert.Equal(9,robot.BehaviorManagerOwner.ActiveSparkId);
        Assert.Equal("SparksRollBlock",runtime.FreeplayObserver.Current?.Id);
        var spark=runtime.FreeplayObserver.Current!;Assert.NotNull(spark.SparkedOwner);
        rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
        Assert.Null(runtime.FreeplayObserver.Current);Assert.Null(spark.SelectedAtSec);
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(10);
        Assert.Same(runtime.FreeplayObserver,robot.BehaviorManagerOwner.Activity);
    }

    [Fact]
    public void ParentSelectionCallbackCannotOverwriteReentrantReplacement()
    {
        using var rig=new M4ControlTests.Rig(ShippedResources());
        rig.Robot.Animations.LoadFrom(ShippedResources());rig.ToSynced();rig.Calibrate();
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp);
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;bool replaced=false;
        void OnLog(string text)
        {
            if(replaced || !text.Contains("SetActivityIDFromSubActivity"))return;
            replaced=true;runtime.AdmitActivity("PutDownDispatch");
        }
        rig.Engine.StackLogLine+=OnLog;
        try
        {
            rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(10);
            Assert.True(replaced);
            Assert.NotSame(runtime.FreeplayObserver,native.BehaviorManagerOwner!.Activity);
            Assert.Null(runtime.FreeplayObserver.Current);
        }
        finally {rig.Engine.StackLogLine-=OnLog;}
    }

    [Fact]
    public void SparkOnChargerDrivesOffThroughCanonicalFreeplay()
    {
        // The m13-driveoff-hw --freeplay route: no pickup, public inputs only. sparksRollBlock.json lists
        // DriveOffCharger first; the rig supplies AnimationStarted/Ended for the spark get-in.
        using var rig=new M4ControlTests.Rig(ShippedResources());
        rig.Robot.Animations.LoadFrom(ShippedResources());rig.ToSynced();rig.Calibrate();
        var flags=RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger;
        rig.State(flags);
        var robot=rig.Engine.Robot!;var runtime=robot.NativeBehaviorRuntime!;var manager=robot.BehaviorManagerOwner!;
        rig.Data(rig.MakeState(flags,origin:robot.CurrentOriginId,frame:robot.PoseFrameId));rig.Tick(10);
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        using var observer=FreeplayStack.CreateNative(rig.Robot);
        int mark=rig.Mark();
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(10);
        observer.ActivateSpark(9);rig.Tick(10);
        var seen=new HashSet<byte>();
        for(int i=0;i<300 && !rig.SentSince(mark).OfType<AppendPathSegmentLine>().Any();i++)
        {
            byte tag=rig.Robot.Animations.CurrentTag;
            if(tag!=0&&seen.Add(tag)){rig.Data(new AnimationStarted{AnimId=tag});rig.Data(new AnimationEnded{AnimId=tag});}
            rig.Data(rig.MakeState(flags,origin:robot.CurrentOriginId,frame:robot.PoseFrameId));rig.Tick(50);
        }
        Assert.Contains(rig.SentSince(mark),message=>message is AppendPathSegmentLine);
        Assert.Equal("SparksRollBlock",runtime.FreeplayObserver.Current?.Id);
        Assert.Same(runtime.Bound["DriveOffCharger"],manager.Current);
        Assert.Equal("DriveOffCharger",observer.Tick(0,0,rig.Robot,null,null).Behavior);
    }

    [Fact]
    public void CanonicalParentKeepsPutdownSubscriptionAfterObserverDispose()
    {
        using var rig=new M4ControlTests.Rig(ShippedResources());
        rig.Robot.Animations.LoadFrom(ShippedResources());rig.ToSynced();rig.Calibrate();
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp);
        rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var runtime=rig.Engine.Robot!.NativeBehaviorRuntime!;
        rig.Robot.ActivateHighLevelActivity("Freeplay");rig.Tick(10);
        var child=Assert.IsType<Activity>(runtime.FreeplayObserver.Current);
        Assert.Equal("PutDownDispatch",child.Id);
        using(var observer=FreeplayStack.CreateNative(rig.Robot))Assert.Same(runtime.FreeplayObserver,observer.Freeplay);
        // Supplied committed off-treads event, through the actual sensor subscriber.
        rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.OnBack,OffTreadsState.OnTreads);
        Assert.Null(runtime.FreeplayObserver.Current);Assert.Null(child.SelectedAtSec);
        Assert.Same(runtime.FreeplayObserver,rig.Engine.Robot.BehaviorManagerOwner!.Activity);
    }

    [Theory]
    [InlineData(true,"DroveAsIntended")] [InlineData(false,"DroveAsIntended")]
    [InlineData(true,null)] [InlineData(false,null)]
    public void ActualRootOrActivityObjectiveInputCannotSilentlyUseShippedInvalidFilter(bool changeRoot,string? objective)
    {
        // Availability guard, not a generic objective implementation. Both inputs
        // pass through actual resources→factory/tree→8B owner→app admission.
        string temporary=Directory.CreateTempSubdirectory("pilot-objective-input-").FullName;
        var missing=new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Observe(string message)=>missing.Enqueue(message);
        SteppedBehavior.MissingReported+=Observe;
        try
        {
            string source=Path.Combine(ShippedResources(),"config","engine","behaviorSystem");
            string destination=Path.Combine(temporary,"config","engine","behaviorSystem");
            foreach(string path in Directory.EnumerateFiles(source,"*.json",SearchOption.AllDirectories))
            {
                string target=Path.Combine(destination,Path.GetRelativePath(source,path));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target,File.ReadAllText(path,System.Text.Encoding.UTF8),System.Text.Encoding.UTF8);
            }
            string changed=changeRoot
                ?Path.Combine(destination,"behaviors","freeplay","putDownDispatch","PutDownDispatch_LookForFaceAndCube.json")
                :Path.Combine(destination,"activities","putDownDispatch.json");
            var json=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(changed,System.Text.Encoding.UTF8),
                documentOptions:new System.Text.Json.JsonDocumentOptions{CommentHandling=System.Text.Json.JsonCommentHandling.Skip,AllowTrailingCommas=true})!;
            const string key="considerThisHasRunForBehaviorObjective";
            if(changeRoot)json[key]=objective;
            else
            {
                var entry=json["behaviorChooser"]!["behaviors"]!.AsArray().Single(node=>node!["behaviorID"]!.GetValue<string>()=="PutDownDispatch_LookForFaceAndCube")!;
                entry["scoring"]![key]=objective;
            }
            File.WriteAllText(changed,json.ToJsonString(),System.Text.Encoding.UTF8);
            using var rig=new M4ControlTests.Rig(temporary);rig.ToSynced();rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
            var owner=rig.Engine.Robot!;var runtime=owner.NativeBehaviorRuntime!;
            var look=Assert.IsType<NativeLookForFaceAndCubeBehavior>(runtime.Bound["PutDownDispatch_LookForFaceAndCube"]);
            Assert.Equal(objective is null,look.ObjectiveInputsAvailable);Assert.Equal(objective is null,look.HasCheckedObjective41);
            rig.Engine.ReportBehaviorObjective(4,"DroveAsIntended","explicit-json-fixture");
            if(objective is null)
            {
                Assert.DoesNotContain(missing,message=>message.Contains("LookForFaceAndCube changed-config objective")||message.Contains("LookForFaceAndCube objective callback unavailable"));
                rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
                Assert.NotNull(owner.BehaviorManagerOwner!.Activity);
                Assert.Equal((byte)13,owner.BehaviorPublicState.State.Activity); // known null preserves41 and does not block actual admission.
            }
            else
            {
                Assert.Contains(missing,message=>message.Contains("changed-config objective input")&&message.Contains(changeRoot?"behavior root":"activity PutDownDispatch"));
                Assert.Contains(missing,message=>message.Contains("objective callback unavailable")&&message.Contains(changeRoot?"behavior root":"activity PutDownDispatch"));
                var priorSelection=owner.BehaviorManagerOwner!.Activity;var priorBehavior=owner.BehaviorManagerOwner.Current;
                Assert.NotNull(priorSelection);
                byte priorActivity=owner.BehaviorPublicState.State.Activity;
                rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
                Assert.Same(priorSelection,owner.BehaviorManagerOwner.Activity);Assert.Same(priorBehavior,owner.BehaviorManagerOwner.Current);
                Assert.Null(owner.ActionList.GetCurrentAction());Assert.False(look.EngineRunning);
                Assert.Equal(priorActivity,owner.BehaviorPublicState.State.Activity); // guard ran before Stop/selection/publication.
            }
        }
        finally{SteppedBehavior.MissingReported-=Observe;Directory.Delete(temporary,true);}
    }

    [Fact]
    public void NullAnimationLibraryDoesNotInventAnExtraDriveOffAdmissionGate()
    {
        // Reach: real configured instance and native base inputs; admission ONLY.
        // No downstream animation/resource success is supplied or asserted.
        using var rig=new M4ControlTests.Rig(ShippedResources());rig.ToSynced();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;
        Assert.Null(rig.Robot.Animations.Library);
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,
            origin:native.CurrentOriginId,frame:native.PoseFrameId));rig.Tick(10);
        Assert.True(runtime.Bound["DriveOffCharger"].IsRunnable(runtime.Context));
        Assert.Null(native.ActionList.GetCurrentAction());
    }

    [Fact]
    public void NativeObserverAndStandaloneFacadeKeepCanonicalOwnersAndOneMoodTick()
    {
        using var rig=new M4ControlTests.Rig(ShippedResources());rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;
        var ai=runtime.Context.AI;var mood=native.MoodOwner!;var needs=rig.Engine.Needs;
        var aiHook=rig.Engine.AIComponentUpdate;var nativeUnset=native.CarryingOwner!.NativeUnset;
        var nativeConfigurations=rig.Robot.Vision.World.NativeConfigurations;
        using var observer=FreeplayStack.CreateNative(rig.Robot);
        Assert.Same(runtime.Context,observer.Context);Assert.Same(native.BehaviorManagerOwner,observer.Manager);
        uint before=mood.HistoryCounts[0];
        _=observer.Tick(999,999000,rig.Robot,null,null);
        _=observer.Freeplay.Tick(999,999000);
        Assert.Equal(before,mood.HistoryCounts[0]);
        using(var publicManipulation=new ManipulationSystem(rig.Robot,rig.Robot.Vision))
        {
            Assert.Same(runtime.Manipulation!.Docking,publicManipulation.Docking);
            var customContext=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap(),Mood=new MoodState(new MoodModel())};
            string obb=Directory.GetParent(Directory.GetParent(ShippedResources())!.FullName)!.FullName;
            using(var standalone=FreeplayStack.CreateStandalone(obb,rig.Robot,customContext,()=>42,rig.Robot.Vision,publicManipulation,withReactions:false))
            {
                Assert.NotSame(native.BehaviorManagerOwner,standalone.Manager);
                Assert.Same(customContext,standalone.Context);
                Assert.NotSame(ai,customContext.AI);
                Assert.Same(ai,runtime.Context.AI);Assert.Same(needs,rig.Engine.Needs);
                Assert.Same(mood,native.MoodOwner);Assert.Same(aiHook,rig.Engine.AIComponentUpdate);
                uint localBefore=customContext.Mood!.HistoryCounts[0];
                _=standalone.Tick(42,42000,rig.Robot,null,null);
                Assert.Equal(localBefore+1,customContext.Mood.HistoryCounts[0]);
                Assert.Equal(42f,customContext.Mood.LastUpdateSeconds);
                Assert.Equal(before,mood.HistoryCounts[0]);
            }
            Assert.Throws<ArgumentException>(()=>FreeplayStack.Create(obb,rig.Robot,runtime.Context,()=>42));
        }
        Assert.Same(nativeUnset,native.CarryingOwner.NativeUnset);
        Assert.Same(nativeConfigurations,rig.Robot.Vision.World.NativeConfigurations);
        Assert.Same(ai,runtime.Context.AI);Assert.Same(mood,runtime.Context.Mood);
        rig.Tick(60);
        Assert.Equal(before+1,mood.HistoryCounts[0]);
    }

    [Fact]
    public void ExplicitConfiguredActivityUsesActualEnginePhaseFactoryAndCompletion()
    {
        // Reach: actual configured factory, app admission, engine manager selection,
        // real action/path and CA/CB/C6 delivery. Firmware motion is represented by
        // explicitly admitted full states; no physical robot result is asserted.
        using var rig=new M4ControlTests.Rig(ShippedResources());
        rig.Robot.Animations.LoadFrom(ShippedResources()); // real loader, joined before admission.
        rig.ToSynced();rig.Calibrate();
        var native=rig.Engine.Robot!;
        var runtime=Assert.IsType<NativeBehaviorRuntime>(native.NativeBehaviorRuntime);
        Assert.NotNull(native.BehaviorManagerOwner!.Activity); // native boot Selection.
        var behavior=Assert.IsType<DriveOffChargerBehavior>(runtime.Bound["DriveOffCharger"]);
        Assert.Same(behavior,native.BehaviorManagerOwner.Find("DriveOffCharger"));
        Assert.Equal(156f,behavior.DriveDistanceMm); // shipped60 + native96, not host ctor fallback.
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,
            origin:native.CurrentOriginId,frame:native.PoseFrameId));rig.Tick(10);
        Assert.Equal(0u,native.LastFullStateResult);Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        int mark=rig.Mark();var order=new List<string>();
        using var objectives=rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,_=>order.Add("objective"));
        behavior.Step+=line=>{if(line=="emotion event DriveOffCharger")order.Add("mood");};
        try
        {
            rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
            Assert.Same(behavior,native.BehaviorManagerOwner.Current);
            Assert.Equal((byte)13,native.BehaviorPublicState.State.Activity);
            Assert.Equal(1,behavior.DrivingAnimationsFlag);
            var look=Assert.IsType<NativeLookForFaceAndCubeBehavior>(runtime.Bound["PutDownDispatch_LookForFaceAndCube"]);
            Assert.False(look.EngineRunning);
            Assert.True(behavior.CurrentRunnerTag!=0);
            var line=Assert.Single(rig.SentSince(mark).OfType<AppendPathSegmentLine>());
            Assert.Equal(new[]{-20f,0f,136f,0f,100f,200f,500f},
                new[]{line.XStartMm,line.YStartMm,line.XEndMm,line.YEndMm,line.Speed.SpeedMmps,line.Speed.AccelMmps2,line.Speed.DecelMmps2});
            ushort id=rig.Robot.Motion.Path.Field42;
            // C7: actual platform-false full state admits the losing retained
            // candidate; real engine selection consumes exactly two shared RNG
            // words before C6 starts any driving animation group.
            int chooserWords=0;
            rig.Engine.ContextRandom.WordSourceForTest=()=>{Interlocked.Increment(ref chooserWords);return 0u;};
            rig.Data(rig.MakeState(origin:native.CurrentOriginId,frame:native.PoseFrameId,x:200));rig.Tick(10);
            Assert.Equal(2,chooserWords);Assert.Same(behavior,native.BehaviorManagerOwner.Current);
            Assert.False(look.EngineRunning);
            rig.Data(new PathFollowingEvent{EventId=id,EventType=0});rig.Tick(10);
            var reported=new HashSet<byte>();
            void CompleteNewAnimation()
            {
                byte tag=rig.Robot.Animations.CurrentTag;
                if(tag!=0 && reported.Add(tag))
                {rig.Data(new AnimationStarted{AnimId=tag});rig.Data(new AnimationEnded{AnimId=tag});}
                rig.Tick(10);
            }
            for(int i=0;i<3;i++)CompleteNewAnimation();
            rig.Data(rig.MakeState(origin:native.CurrentOriginId,frame:native.PoseFrameId,x:200));rig.Tick(10);
            rig.Data(new PathFollowingEvent{EventId=id,EventType=2});rig.Tick(10);
            for(int i=0;i<12 && behavior.DriveResult is null;i++)CompleteNewAnimation();
            Assert.Equal(ActionResult.Success,behavior.DriveResult);
            Assert.Equal(new[]{"objective","mood"},order);
            Assert.False(behavior.HasCurrentAction); // synchronous 5A clears before callback.
            rig.Tick(10);
            Assert.NotNull(behavior.DroveOffAtSec);
            Assert.Equal((float)behavior.DroveOffAtSec!,runtime.Context.LastDriveOffChargerSec);
        }
        finally{rig.Engine.ContextRandom.WordSourceForTest=null;native.BehaviorManagerOwner.Stop(BehaviorStopReason.Cancelled,rig.Engine.Timer.SecondsF);native.ActionList.Clear();}
    }

    [Fact]
    public void HikingAliasManualEntryUsesRealActionAndCompletion()
    {
        // Reach: actual configured factory and explicit public manager entry,
        // real action/path and CA/CB/C6 delivery. Firmware motion is represented by
        // explicitly admitted full states; no physical robot result is asserted.
        using var rig=new M4ControlTests.Rig(ShippedResources());
        rig.Robot.Animations.LoadFrom(ShippedResources()); // real loader, joined before admission.
        rig.ToSynced();rig.Calibrate();
        var native=rig.Engine.Robot!;
        var runtime=Assert.IsType<NativeBehaviorRuntime>(native.NativeBehaviorRuntime);
        Assert.NotNull(native.BehaviorManagerOwner!.Activity); // native boot Selection.
        var behavior=Assert.IsType<DriveOffChargerBehavior>(runtime.Bound["Hiking_DriveOffCharger"]);
        Assert.Same(behavior,native.BehaviorManagerOwner.Find("Hiking_DriveOffCharger"));
        Assert.Equal(141f,behavior.DriveDistanceMm); // shipped45 + native96.
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,
            origin:native.CurrentOriginId,frame:native.PoseFrameId));rig.Tick(10);
        Assert.Equal(0u,native.LastFullStateResult);Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        int mark=rig.Mark();var order=new List<string>();
        using var objectives=rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{Assert.Equal(4u,Assert.IsType<uint>(p));order.Add("objective");});
        behavior.Step+=line=>{if(line=="emotion event DriveOffCharger")order.Add("mood");};
        try
        {
            rig.Robot.ExecuteBehaviorById((byte)BehaviorID.Hiking_DriveOffCharger);rig.Tick(10);
            Assert.Same(behavior,native.BehaviorManagerOwner.Current);
            Assert.Equal(1,behavior.DrivingAnimationsFlag);
            Assert.True(native.BehaviorManagerOwner.HasDisableLock((ReactionTrigger)0,"Hiking_DriveOffCharger_behaviorLock"));
            Assert.Contains(rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="Hiking_DriveOffCharger");
            Assert.True(behavior.CurrentRunnerTag!=0);
            var line=Assert.Single(rig.SentSince(mark).OfType<AppendPathSegmentLine>());
            Assert.Equal(new[]{-20f,0f,121f,0f,100f,200f,500f},
                new[]{line.XStartMm,line.YStartMm,line.XEndMm,line.YEndMm,line.Speed.SpeedMmps,line.Speed.AccelMmps2,line.Speed.DecelMmps2});
            ushort id=rig.Robot.Motion.Path.Field42;
            rig.Data(rig.MakeState(origin:native.CurrentOriginId,frame:native.PoseFrameId,x:200));rig.Tick(10);
            Assert.Same(behavior,native.BehaviorManagerOwner.Current);
            rig.Data(new PathFollowingEvent{EventId=id,EventType=0});rig.Tick(10);
            var reported=new HashSet<byte>();
            void CompleteNewAnimation()
            {
                byte tag=rig.Robot.Animations.CurrentTag;
                if(tag!=0 && reported.Add(tag))
                {rig.Data(new AnimationStarted{AnimId=tag});rig.Data(new AnimationEnded{AnimId=tag});}
                rig.Tick(10);
            }
            for(int i=0;i<3;i++)CompleteNewAnimation();
            rig.Data(rig.MakeState(origin:native.CurrentOriginId,frame:native.PoseFrameId,x:200));rig.Tick(10);
            rig.Data(new PathFollowingEvent{EventId=id,EventType=2});rig.Tick(10);
            for(int i=0;i<12 && behavior.DriveResult is null;i++)CompleteNewAnimation();
            Assert.Equal(ActionResult.Success,behavior.DriveResult);
            Assert.Equal(new[]{"objective","mood"},order);
            Assert.False(behavior.HasCurrentAction); // synchronous 5A clears before callback.
            rig.Tick(10);
            Assert.NotNull(behavior.DroveOffAtSec);
            Assert.Equal((float)behavior.DroveOffAtSec!,runtime.Context.LastDriveOffChargerSec);
            native.BehaviorManagerOwner.Stop(BehaviorStopReason.Cancelled,rig.Engine.Timer.SecondsF);
            Assert.False(native.BehaviorManagerOwner.HasDisableLock((ReactionTrigger)0,"Hiking_DriveOffCharger_behaviorLock"));
            Assert.DoesNotContain(rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="Hiking_DriveOffCharger");
        }
        finally{native.BehaviorManagerOwner.Stop(BehaviorStopReason.Cancelled,rig.Engine.Timer.SecondsF);native.ActionList.Clear();}
    }

    [Fact]
    public void HikingConfiguredAliasAndParentRemainDistinctFromAdmission()
    {
        // Shipped asset and retained owners only; no chooser victory is asserted.
        using var rig=new M4ControlTests.Rig(ShippedResources());rig.ToSynced();
        var native=rig.Engine.Robot!;var runtime=native.NativeBehaviorRuntime!;
        var alias=Assert.IsType<DriveOffChargerBehavior>(runtime.Bound["Hiking_DriveOffCharger"]);
        Assert.Same(alias,native.BehaviorManagerOwner!.Find("Hiking_DriveOffCharger"));
        Assert.Equal(141f,alias.DriveDistanceMm);
        Assert.Equal(156f,Assert.IsType<DriveOffChargerBehavior>(runtime.Bound["DriveOffCharger"]).DriveDistanceMm);
        var hiking=NativeBehaviorRuntime.AllActivities(runtime.Tree).Single(a=>a.Id=="Hiking");
        var chooser=Assert.IsType<ScoringChooser>(hiking.Chooser);
        Assert.Equal(new[]{"ReactToObstacle","KnockOverCubes","Hiking_DriveOffCharger"},chooser.Entries.Take(3).Select(e=>e.BehaviorId));
        Assert.Equal(new[]{30d,20d,10d},chooser.Entries.Take(3).Select(e=>e.FlatScore));
        Assert.Equal(1d,chooser.ScoreBonusForCurrent!.EvaluateY(0));
        Assert.Equal(BehaviorChooserType.StrictPriority,hiking.InterludeChooser!.Type);
        Assert.Equal("Simple",hiking.Strategy.Type);Assert.Equal(60d,hiking.Strategy.ShouldEndDurationSec);Assert.Equal(15d,hiking.Strategy.CooldownBaseSec);
        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,origin:native.CurrentOriginId,frame:native.PoseFrameId));rig.Tick(10);
        Assert.True(alias.IsRunnable(runtime.Context));
        Assert.Null(native.ActionList.GetCurrentAction());
    }

    [Fact]
    public void UnavailableHikingAdmissionRetainsPriorActivityWithoutMotion()
    {
        // Explicit host activity requests; no native Hiking parent selection is claimed.
        using var rig=new M4ControlTests.Rig(ShippedResources());rig.ToSynced();rig.State();
        var native=rig.Engine.Robot!;var missing=new List<string>();
        void Observe(string message)=>missing.Add(message);
        SteppedBehavior.MissingReported+=Observe;
        try
        {
            rig.Robot.ActivateHighLevelActivity("PutDownDispatch");rig.Tick(10);
            var prior=Assert.IsAssignableFrom<IManagedActivity>(native.BehaviorManagerOwner!.Activity);
            int mark=rig.Mark();
            rig.Robot.ActivateHighLevelActivity("Hiking");rig.Tick(10);
            Assert.Same(prior,native.BehaviorManagerOwner.Activity);
            Assert.DoesNotContain(rig.SentSince(mark),m=>m is AppendPathSegmentLine || m is ExecutePath);
            Assert.Contains(missing,m=>m.Contains("admission Hiking") && m.Contains("M15-006/M15-008") && m.Contains("prior activity retained"));
        }
        finally{SteppedBehavior.MissingReported-=Observe;}
    }

    [Theory]
    [InlineData(96f,176f,100f)]
    [InlineData(-96f,-16f,-80f)]
    public void RealStraightDispatchUsesStoredForwardPoseAndSourceProfile(float distance,float expectedEnd,float speed)
    {
        // Reach: actual full-state admission, ActionList/Path dispatch and received C6;
        // explicit host queue admission and NoAnimation triplet are fixture inputs.
        // 0x0051508C: no-carry SetPose shifts -20. 0x0054762A/3C reads
        // current main yaw and STORED forward XY, not a later carry recomputation.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Calibrate();
        var native=rig.Engine.Robot!;
        rig.Data(rig.MakeState(origin:native.CurrentOriginId,frame:native.PoseFrameId,x:100,y:200));rig.Tick(10);
        Assert.Equal(0u,native.LastFullStateResult);
        Assert.Equal(80f,(float)native.ForwardPose.Translation.X);
        rig.Robot.Animations.PushDrivingAnimations((AnimationTrigger)0x23F,(AnimationTrigger)0x23F,(AnimationTrigger)0x23F,"explicit-fixture-input");
        rig.Tick(10);
        native.CarryingOwner!.SetCarrying(0); // mutate AFTER SetPose; zero is a real raw carried ID.
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        var action=new DriveStraightAction(m,distance);
        var reports=new List<RobotCompletedAction>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)reports.Add(r);});
        int mark=rig.Mark();
        try
        {
            Assert.Equal(0,native.ActionList.QueueAction(QueueActionPosition.Now,action,0));rig.Tick(10);
            var sent=rig.SentSince(mark);
            var line=Assert.Single(sent.OfType<AppendPathSegmentLine>());
            Assert.Equal(new[]{80f,200f,expectedEnd,200f,speed,200f,500f},
                new[]{line.XStartMm,line.YStartMm,line.XEndMm,line.YEndMm,line.Speed.SpeedMmps,line.Speed.AccelMmps2,line.Speed.DecelMmps2});
            int clear=sent.FindIndex(m=>m is ClearPath), append=sent.FindIndex(m=>m is AppendPathSegmentLine),execute=sent.FindIndex(m=>m is ExecutePath);
            Assert.True(clear>=0 && clear<append && append<execute);
            ushort id=rig.Robot.Motion.Path.Field42;
            rig.Data(new PathFollowingEvent{EventId=id,EventType=0});rig.Tick(10);
            Assert.Equal(3,rig.Robot.Motion.Path.DriveToPoseStatus);
            rig.Data(new PathFollowingEvent{EventId=id,EventType=2});rig.Tick(10);
            var report=Assert.Single(reports.Where(r=>r.Tag==action.Tag));
            Assert.Equal(0u,report.Result);
            Assert.Equal((byte)6,report.CompletionVariant);
            Assert.Equal((byte)6,report.PackBody()[^1]); // U1: literal defaultCompleted, no raw U32.
        }
        finally{native.ActionList.Cancel(action.Tag);}
    }
}
