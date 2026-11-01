using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// One actual Robot owns the configured instances and source inputs. Construction
// does not select an app activity or synthesize a Freeplay admission.
internal sealed class NativeBehaviorRuntime : IDisposable
{
    private readonly CozmoRobot _facade;
    private readonly EngineRobot _robot;
    private readonly BehaviorManager _manager;
    internal BehaviorContext Context {get;}
    internal ManipulationSystem? Manipulation {get;}
    internal NativeConfigurationManager Configurations {get;}
    internal AIComponent AI {get;}
    internal NativePickupTargetCache? PickupTargets {get;}
    // Native input boundaries; canonical pyramid suppliers resolve the live owner at each call.
    // Tests can replace these delegates for isolated lifecycle checks.
    // Bound below to the actual configuration owner; tests may replace this seam.
    internal Func<ITallStackWeakTarget?>? TallStackWeakInput {get;set;}
    // The manager's configured behavior owns the first recipient (0050FCAC/BE).
    internal TallStackDelocalizationData? TallStackOwnerInput {get;set;}
    private sealed class CheckedTallStackWeak(ITallStackWeakTarget target, Func<bool> available) : ITallStackWeakTarget
    {
        public bool TryLock(out TallStackLease? lease)
        {
            if (!available()) { lease=null; return false; }
            return target.TryLock(out lease);
        }
    }
    internal Func<int,uint>? PyramidTargetInput {get;set;}
    internal Func<IReadOnlyList<NativePyramidBase>>? PyramidBaseStrongInput {get;set;}
    internal Func<IReadOnlyList<NativePyramidConfiguration>>? PyramidFullStrongInput {get;set;}
    internal IReadOnlyDictionary<string,IBehavior> Bound {get;}
    private readonly SortedDictionary<BehaviorID,byte> _executableMetadata=new();
    internal IReadOnlyList<Activity> Tree {get;}
    internal FreeplayDataTracker Tracker {get;}
    // SetupTurnAndVerify551FE6..5520BA: actual owner/located target, finite child only.
    internal NativeVisuallyVerifyNoObjectRunner CreatePickupVisualVerification(uint targetId)
    {
        if(_retired||Manipulation is not {} m)throw new InvalidOperationException("MISSING: native pickup visual owner retired/unavailable");
        using var mutation=m.World.EnterPoseMutation();
        var target=m.World.GetLocatedObjectByIdHelper(targetId)??throw new InvalidOperationException("MISSING: native pickup visual target unavailable");
        using var flat=target.NativePose.GetWithRespectToRoot();
        var dimensions=NativeConfigurationGeometry.Dimensions(target,flat);
        var t=flat.Translation;float z=(float)t.Z+(float)dimensions.Z;
        using var root=target.NativePose.FindRoot();
        using var raised=new Pose3d(flat.Quaternion,new Vec3((float)t.X,(float)t.Y,z),parent:root);
        var child=new NativeVisuallyVerifyNoObjectRunner(m,raised,new Vec3((float)dimensions.X*0.5f,(float)dimensions.Y*0.5f,(float)dimensions.Z*0.5f));
        child.AddIgnoreId(targetId);return child;
    }

    private NativeConfigurationSnapshot RequirePyramidSnapshot()
    {
        if(_retired || !ReferenceEquals(_robot.Engine.Robot,_robot) || Manipulation is not {} m ||
            !ReferenceEquals(m.World,_facade.LocalizationWorld) ||
            !ReferenceEquals(m.World.NativeConfigurations,Configurations) || !Configurations.InputsAvailable)
            throw new NativeConfigurationUnavailable("M12-049 pyramid actual configuration owner or inputs unavailable");
        return Configurations.Snapshot;
    }

    private Activity? _selected;
    private int _highLevel=4; // constructor Count, then configured boot selects Selection
    internal float HighLevelEntryTime {get;private set;}=-1f;
    // Continuation of the missing parent picker, not an ActivitySparked state/Complete latch.
    internal bool AwaitingParentActivityPick {get;private set;}
    private bool _retired;
    private long _admissionEpoch;
    private readonly Action<bool> _offTreadsPause,_chargerPause;
    private readonly VisionSystem? _ownedVision;
    private readonly VisionSystem? _vision;
    private readonly OverheadEdgesDetector? _ownedOverhead;
    private readonly Func<bool>? _workoutMusic;

    internal NativeBehaviorRuntime(CozmoRobot facade,EngineRobot robot,BehaviorManager manager,
        BehaviorContext context,VisionSystem? vision)
    {
        _facade=facade;_robot=robot;_manager=manager;Context=context;_vision=vision;
        Context.Mood=robot.MoodOwner;
        AI=new(()=>unchecked((int)robot.CurrentOriginId),facade.Engine.EmitStackLog,
            (l,c,k,t)=>facade.Engine.EmitSourceLog(l,c,k,t),facade.Engine.Needs.SevereNeeds);
        Context.AI=AI;
        if (facade.Engine.Options.ResourcesPath is {} workoutResources)
        {
            string path=Path.Combine(workoutResources,"config","engine","behaviorSystem","workout_config.json");
            try { if(File.Exists(path)) AI.Workout=WorkoutComponent.Parse(File.ReadAllText(path),facade.Engine.EmitStackLog); }
            catch(WorkoutConfigurationException error)
            { AI.Workout=error.Partial; SteppedBehavior.ReportMissing("M13-010 canonical workout configuration unavailable: "+error.Message); }
            catch(Exception error) when(error is IOException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
            { SteppedBehavior.ReportMissing("M13-010 canonical workout configuration unavailable: "+error.Message); }
        }
        if(AI.Workout is {} workout && workout.GetCurrentWorkout() is not null && robot.MoodOwner is {} mood)
        {
            workout.TriggerEmotionEvent=(name,now)=>mood.Trigger(name,now);
            workout.ClockSec=()=>robot.Engine.Timer.SecondsF;
            _workoutMusic=()=>workout.ShouldPlayEightiesMusic(e=>mood[e],e=>mood.GetHistoryValueTicksAgo(e,60),()=>robot.Engine.ContextRandom.RandDbl(1));
            robot.EightiesMusicInput=_workoutMusic;
        }
        else SteppedBehavior.ReportMissing("M13-010 canonical workout configuration or MoodManager unavailable; no workout or music decision substituted");
        AI.BindDestructionRecipient(0x1C,()=>
        {
            if(ReferenceEquals(robot.EightiesMusicInput,_workoutMusic))robot.EightiesMusicInput=null;
            AI.Workout=null;
            AI.NativeTeardown.Destroy(0x1C); // remaining native recursive finalizer is still M15-045
        });
        Tracker=new(()=>unchecked((ulong)facade.Engine.Timer.Nanoseconds))
        {SourceLog=(l,c,k,t)=>facade.Engine.EmitSourceLog(l,c,k,t)};
        AI.Tracker=Tracker;
        AI.BindDestructionRecipient(0x2C,Tracker.DestroyStorage);
        robot.Lifetime.Bind(-2,_=>Tracker.ForceUpdate()); // 0x0051111A, before AbortAll
        _offTreadsPause=paused=>Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads,paused);
        _chargerPause=paused=>Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OnCharger,paused);
        facade.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads=_offTreadsPause;
        facade.Sensors.OnChargerPlatformChangedInternal+=_chargerPause;
        Configurations=new(facade.LocalizationWorld,robot);
        facade.LocalizationWorld.NativeConfigurations=Configurations;
        if(vision is not null)
        {
            Manipulation=new(facade,vision,robot){ClockSec=()=>facade.Engine.Timer.SecondsF};
            if(facade.Engine.Options.ResourcesPath is {} plannerResources)Manipulation.LoadNativePlanner(plannerResources);
            PickupTargets=new NativePickupTargetCache(Manipulation,robot,RequirePyramidSnapshot);
            PyramidTargetInput=PickupTargets.GetBest;
            PyramidBaseStrongInput=()=>RequirePyramidSnapshot().Bases;
            PyramidFullStrongInput=()=>RequirePyramidSnapshot().Pyramids;
            if(vision.OverheadEdges is null)
            {
                _ownedVision=vision;_ownedOverhead=new OverheadEdgesDetector();vision.OverheadEdges=_ownedOverhead;
            }
        }
        var factory=new BehaviorFactoryContext{Vision=vision,Manipulation=Manipulation,NativeRobot=robot,Features=facade.Engine.Features,
            TallStackData=robot.TallStackContainers.Length == 2 ? robot.TallStackContainers[0] : null,
            SourceLog=(l,c,k,t)=>facade.Engine.EmitSourceLog(l,c,k,t),Log=facade.Engine.EmitStackLog};
        var container=new BehaviorContainer(new BehaviorFactory(),factory,
            facade.Engine.HostBehaviorMap??new Dictionary<BehaviorID,System.Text.Json.JsonElement>());
        // fidelity: M15-008 -- 005BBAE8/005BBC9C uses the container's retained config winner.
        Context.NeedsActionIds=BehaviorNeedsActions.FromConfiguredMap(
            facade.Engine.HostBehaviorMap??new Dictionary<BehaviorID,System.Text.Json.JsonElement>());
        var bound=new Dictionary<string,IBehavior>(StringComparer.Ordinal);
        foreach(var behavior in container.Behaviors.Values)
        {
            bound.Add(behavior.Id,behavior);
            if(behavior is SteppedBehavior stepped)stepped.Clock=()=>facade.Engine.Timer.SecondsF*1000.0;
            if(manager.Find(behavior.Id) is null)manager.Add(behavior);
        }
        Bound=bound;
        foreach(var entry in facade.Engine.HostBehaviorMap??new Dictionary<BehaviorID,System.Text.Json.JsonElement>())
            _executableMetadata[entry.Key]=bound.TryGetValue(entry.Key.ToString(),out var b) && b is SteppedBehavior {NativeBaseInputs.CheckedExecutableType: {} value}
                ? value : ExecutableBehaviorNames.FromConfig(entry.Value);
        manager.BindConfiguredBehaviors(Bound);
        // Manager's container is constructed before the BSM container (0050FCAC/BE).
        if (bound.TryGetValue("CantHandleTallStack", out var tall) && tall is NativeCantHandleTallStackBehavior && robot.TallStackContainers.Length == 2)
            TallStackOwnerInput=robot.TallStackContainers[0];
        bool TallStackAvailable() =>
            !_retired && ReferenceEquals(robot.Engine.Robot,robot) && ReferenceEquals(robot.NativeBehaviorRuntime,this) &&
            Manipulation is {} m && ReferenceEquals(m.World,facade.LocalizationWorld) &&
            ReferenceEquals(m.World.NativeConfigurations,Configurations) && !Configurations.Retired && Configurations.InputsAvailable;
        TallStackWeakInput=()=>
        {
            if (!TallStackAvailable())
                throw new NativeConfigurationUnavailable("actual TallStack configuration owner or inputs unavailable");
            var target=Configurations.Tallest()?.WeakTarget;
            return target is null ? null : new CheckedTallStackWeak(target,TallStackAvailable);
        };
        string? resources=facade.Engine.Options.ResourcesPath;
        if(resources is not null && Directory.Exists(Path.Combine(resources,"config","engine","behaviorSystem","activities"))
            &&File.Exists(Path.Combine(resources,"config","engine","behaviorSystem","activities_config.json")))
        {
            Tree=ActivityTreeLoader.LoadResources(resources,Bound,manager.Penalty,robot);
            foreach(var activity in AllActivities(Tree))
            {
                activity.Analyzer=AI.Analyzer;
                foreach(var chooser in new[]{activity.Chooser,activity.InterludeChooser})
                    if(chooser is SelectionChooser selectionChooser)
                    {
                        selectionChooser.SourceLog=(level,channel,key,text)=>facade.Engine.EmitSourceLog(level,channel,key,text);
                        selectionChooser.FindBehaviorByExecutableType=type=>
                        {
                            foreach(var entry in _executableMetadata)
                            {
                                if(entry.Value!=type)continue;
                                if(Bound.TryGetValue(entry.Key.ToString(),out var found))return found;
                                throw new NativeChooserUnavailable("configured executable owner "+entry.Key+" is unbuilt");
                            }
                            return null;
                        };
                        selectionChooser.SetAnalyzerEnabled=(behavior,key,enabled)=>
                        {
                            if(behavior is not SteppedBehavior stepped)
                            {SteppedBehavior.ReportMissing("M8-013 Selection SetProcessEnabled 0x0060AE84: non-stepped native process field is unbuilt");return;}
                            if(enabled)AI.Analyzer.AddEnableRequest(stepped.RequiredProcessId,key);
                            else AI.Analyzer.RemoveEnableRequest(stepped.RequiredProcessId,key);
                        };
                    }
            }
            robot.NativeActivityRecipientsConstructed=true;
            if(Tree.Any(a=>a.Id=="Freeplay"))
            {
                _ = FreeplayObserver;
                FreeplayObserver.Freeplay.NativeDeselectedHook=FreeplayObserver.ParentDeselected;
                // fidelity: M15-060 -- ctor005AD3B4 subscribes only with external interface.
                if(robot.HasExternalInterface)facade.Sensors.FreeplayOffTreadsStateChanged += OnFreeplayTreads;
            }
            _selected=Tree.FirstOrDefault(a=>a.Id=="Selection");
            if(_selected is {} selection)
            {
                _highLevel=2;
                selection.GuardNativeLifecycle=true;
                selection.OnSelected(facade.Engine.Timer.SecondsF,facade.Engine.EmitStackLog);
                HighLevelEntryTime=facade.Engine.Timer.SecondsF;
                Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,true);
                _manager.Activity=new ConfiguredActivity(selection,_robot,_manager,Context,()=>{},()=>false,()=>{});
                // SetCurrentActivity(Selection,true) takes the normal None-trigger chooser tail before Robot publication.
                _manager.ChooseAfterHighLevelChange(HighLevelEntryTime);
            }
        }
        else
        {
            Tree=Array.Empty<Activity>();
            SteppedBehavior.ReportMissing("Native configured activity resources unavailable; no activity/tree/default selection substituted");
        }
    }

    internal static IEnumerable<Activity> AllActivities(IEnumerable<Activity> roots)
    {
        foreach(var activity in roots)
        {yield return activity;foreach(var child in AllActivities(activity.SubActivities))yield return child;}
    }

    internal void AdmitActivity(string activityId)
    {
        if(activityId=="Freeplay") { AdmitFreeplay(); return; }
        if(activityId=="Selection") { AdmitSelection(); return; }
        var selected=AllActivities(Tree).FirstOrDefault(a=>a.Id==activityId)
            ??throw new ArgumentException("No configured activity named "+activityId,nameof(activityId));
        if(selected.SparkedOwner is {CanExecute:false})
        {
            SteppedBehavior.ReportMissing("M15-059 changed Sparked parameter configuration requires native retained Wait or subActivityDelegate lifecycle, which is unbuilt; prior selected activity retained before admission");
            return;
        }
        if(selected.SparkedOwner is null && (selected.Id!="PutDownDispatch" || selected.Type!="BehaviorsOnly" || selected.NativeUpdate20 is null || selected.InterludeChooser is not null))
        {
            if(selected.Id=="NeedsSevereLowEnergy" || selected.Id=="NeedsSevereLowRepair")
            {
                SteppedBehavior.ReportMissing("M8-013 " + selected.Id + " admission: native NeedsExpression activity owner is unbound; prior activity retained");
                return;
            }
            SteppedBehavior.ReportMissing(selected.Id=="Hiking"
                ? "Native activity admission Hiking: specialized lifecycle/interlude is not bound; prior activity retained (M15-006/M15-008)"
                : "Native activity admission "+activityId+": specialized lifecycle/interlude is not bound; prior activity retained (M15-061 for Feeding)");
            return;
        }
        if(selected.Chooser?.BehaviorIds.Any(id=>Bound.TryGetValue(id,out var candidate)
            &&candidate is NativeLookForFaceAndCubeBehavior look&&!look.HasCheckedObjective41)==true)
        {
            SteppedBehavior.ReportMissing("Native PutDownDispatch admission: LookForFaceAndCube explicit objective input lacks checked invalid41 contract; prior activity retained before selection/motion");
            return;
        }
        long epoch=++_admissionEpoch;
        float now=_facade.Engine.Timer.SecondsF;
        if(_selected?.Id=="Freeplay")
        {
            _selected.OnDeselected(now,_facade.Engine.EmitStackLog);
            if(_retired||epoch!=_admissionEpoch)return;
            _selected=null;
        }
        _manager.Stop(BehaviorStopReason.Interrupted,now);
        if(_retired||epoch!=_admissionEpoch)return;
        _selected?.OnDeselected(now,_facade.Engine.EmitStackLog);
        if(_retired||epoch!=_admissionEpoch)return;
        _selected=selected;
        _highLevel=-1; // explicit host subactivity admission is not a high-level enum
        AwaitingParentActivityPick=false;
        selected.OnSelected(now,_facade.Engine.EmitStackLog);
        if(_retired||epoch!=_admissionEpoch||!ReferenceEquals(_selected,selected))return;
        Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,selected.Id!="Freeplay");
        // Spark is written only by the unbuilt specialized Freeplay selection path 0x005AE4E2..F2.
        _manager.Activity=new ConfiguredActivity(selected,_robot,_manager,Context,()=>WaitForParentPicker(selected),()=>AwaitingParentActivityPick,()=>RetireUnavailable(selected,epoch));
    }
    private void AdmitSelection()
    {
        if(_highLevel==2)return;
        var selection=Tree.FirstOrDefault(a=>a.Id=="Selection");
        if(selection is null){SteppedBehavior.ReportMissing("M15-031 configured Selection owner unavailable");return;}
        float now=_facade.Engine.Timer.SecondsF;long epoch=++_admissionEpoch;
        _selected?.OnDeselected(now,_facade.Engine.EmitStackLog);
        if(_retired||epoch!=_admissionEpoch)return;
        bool ordinary=_manager.PrepareHighLevelChange(now);
        if(_retired||epoch!=_admissionEpoch)return;
        _selected=selection;_highLevel=2;AwaitingParentActivityPick=false;
        selection.OnSelected(now,_facade.Engine.EmitStackLog);
        if(_retired||epoch!=_admissionEpoch)return;
        HighLevelEntryTime=now;Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,true);
        _manager.Activity=new ConfiguredActivity(selection,_robot,_manager,Context,()=>{},()=>false,()=>{});
        if(ordinary)_manager.ChooseAfterHighLevelChange(now);
        _manager.HighLevelAdapterTail(false,now);
    }
    private void OnFreeplayTreads(OffTreadsState from,OffTreadsState to)
    {
        if(to==OffTreadsState.OnTreads && from!=OffTreadsState.OnTreads)
            FreeplayObserver.OnRobotPutDown(_facade.Engine.Timer.SecondsF);
    }

    private void AdmitFreeplay()
    {
        float now=_facade.Engine.Timer.SecondsF;
        if(_highLevel!=1)
        {
            long epoch=++_admissionEpoch;
            if(_selected is {} old)
            {
                old.OnDeselected(now,_facade.Engine.EmitStackLog);
            }
            else SteppedBehavior.ReportMissing("M15-031 boot Selection lifecycle remains unbound before Freeplay admission");
            if(_retired||epoch!=_admissionEpoch)return;
            bool ordinary=_manager.PrepareHighLevelChange(now);
            if(_retired||epoch!=_admissionEpoch)return;
            _selected=FreeplayObserver.Freeplay;_highLevel=1;AwaitingParentActivityPick=false;
            _selected.OnSelected(now,_facade.Engine.EmitStackLog);
            if(_retired||epoch!=_admissionEpoch)return;
            HighLevelEntryTime=now;
            Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,false);
            _manager.Activity=FreeplayObserver;
            RefreshFreeplayInputs(now);
            if(ordinary)_manager.ChooseAfterHighLevelChange(now);
        }
        _manager.HighLevelAdapterTail(true,now);
    }

    private void RetireUnavailable(Activity activity,long epoch)
    {
        if(_retired||epoch!=_admissionEpoch||!ReferenceEquals(_selected,activity))return;
        ++_admissionEpoch;_selected=null;AwaitingParentActivityPick=false;
        _manager.Activity=null;
        // Host-only unsupported evaluation: release the detached old resources
        // before stopping, so replacement during Stop cannot strand them.
        activity.OnDeselected(_facade.Engine.Timer.SecondsF,_facade.Engine.EmitStackLog);
        if(_retired||_admissionEpoch!=epoch+1)return;
        _manager.Stop(BehaviorStopReason.Interrupted,_facade.Engine.Timer.SecondsF);
        if(_retired||_admissionEpoch!=epoch+1)return;
        SteppedBehavior.ReportMissing("M15-059 "+activity.Id+" chooser unavailable input: owned activity retired without native completion");
    }

    private void WaitForParentPicker(Activity activity)
    {
        if(!ReferenceEquals(_selected,activity))return;
        AwaitingParentActivityPick=true;
        SteppedBehavior.ReportMissing("M15-006 Sparked desired returned null: native Freeplay PickNewActivityForSpark 0x005AE70A..005AE760 is unbuilt; actual selected owner/manager activity/state retained at parent continuation. OnDeselected requires a changed selected candidate (0x005ADE02); explicit supplied next admission may resume this boundary; no fallback or repeated Complete");
    }

    internal FreeplayDecision Observe()=>new(_facade.Engine.Timer.SecondsF,_selected?.Id=="Freeplay"?_observer?.Current?.Id:_selected?.Id,_manager.Current?.Id,"native engine observation");
    private FreeplaySystem? _observer;
    internal FreeplaySystem FreeplayObserver=>_observer??=new FreeplaySystem(_manager,Context,
        Tree.FirstOrDefault(a=>a.Id=="Freeplay")??throw new InvalidOperationException("Configured Freeplay resources unavailable"),
        Bound,new FreeplayInputs{Needs=_facade.Engine.Needs,Mood=_robot.MoodOwner,Features=_facade.Engine.Features},false)
        {ObserveOnly=Observe,NativeOwner=true,NativeStillCurrent=()=>!_retired && _selected?.Id=="Freeplay" && ReferenceEquals(_manager.Activity,_observer),SparkPauseChanged=p=>Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.Spark,p)};
    internal bool LookInputsAvailable(NativeLookForFaceAndCubeBehavior look)=>
        !_retired && ReferenceEquals(_robot.Engine.Robot,_robot) && ReferenceEquals(_robot.NativeBehaviorRuntime,this) &&
        Manipulation is {} m && _vision is not null && ReferenceEquals(m.Vision,_vision) &&
        ReferenceEquals(m.World,_facade.LocalizationWorld) && _observer is not null &&
        _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null && _robot.MainPose.Node is not null &&
        _observer.Freeplay.DesiredActivityNames is not null &&
        ReferenceEquals(_observer.Freeplay,Tree.FirstOrDefault(a=>a.Id=="Freeplay")) &&
        Bound.TryGetValue(look.Id,out var bound) && ReferenceEquals(bound,look);
    internal bool CalculateLookDesired(NativeLookForFaceAndCubeBehavior look)=>
        LookInputsAvailable(look) && _observer!.CalculateNativeDesired(_robot,Manipulation!,()=>LookInputsAvailable(look));
    private void RefreshFreeplayInputs(float now)=>FreeplayObserver.RefreshInputs(_facade,_vision,Manipulation,now,_robot);
    internal void UpdateAI()=>AI.Update(_facade);
    // The existing checked 0x006553FC -> 0x0067F7AC recipient belongs to the
    // actual world, independently of a host Freeplay handle and before image E8.
    internal void ApplyOverhead(OverheadEdgeFrame edges,VisionSystem vision)
    {
        if(!edges.GroundPlaneValid || edges.Chains.Count==0)return;
        var world=vision.World;
        LogCallbackDeferral.RunGated(world.FrameGate,()=>
        {
            if(world.Maps.Current is not {} map)return;
            if(!_robot.TryGetNativeOriginOwner(world.Maps.CurrentOrigin,out var origin))
            {SteppedBehavior.ReportMissing("M11-079 overhead map actual origin unavailable; no MissingOrigin fallback");return;}
            var previous=map.OverheadContext;
            map.OverheadContext=new OverheadMapContext
            {
                History=timestamp=>OverheadHistorySample.Read(vision.History,timestamp),
                Origin=()=>origin!,
                ImageTimestamp=()=>_facade.CameraSettings.LastProcessedImageTimestamp,
                Clear=Manipulation is {} clearOwner?clearOwner.Whiteboard.ProcessClearQuad:null,
                WhiteboardState=Manipulation is {} stateOwner?stateOwner.Whiteboard.SetOverheadEdgeState:null,
            };
            try{map.AddVisionOverheadEdges(edges,_robot.MainPose);}
            finally{map.OverheadContext=previous;}
        });
    }
    internal void UpdateManager(float capturedNow)
    {
        if(AwaitingParentActivityPick)return;
        Context.Map=_facade.LocalizationWorld.Maps.Current;
        if(_observer is not null && ReferenceEquals(_manager.Activity,_observer))RefreshFreeplayInputs(capturedNow);
        _manager.Update(capturedNow*1000.0,capturedNow);
    }

    public void Dispose()
    {
        if(_retired)return;_retired=true;++_admissionEpoch;
        _facade.Sensors.FreeplayOffTreadsStateChanged -= OnFreeplayTreads;
        if(ReferenceEquals(_facade.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads,_offTreadsPause))
            _facade.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads=null;
        _facade.Sensors.OnChargerPlatformChangedInternal-=_chargerPause;
        if(_ownedVision is {} vision&&ReferenceEquals(vision.OverheadEdges,_ownedOverhead))vision.OverheadEdges=null;
        foreach(var activity in AllActivities(Tree))if(activity.GuardNativeLifecycle)activity.RetireHostResources();
        _selected=null;
        // The manager is stopped/disposed by its actual lifetime owner first.
        _observer?.DetachHostHandle();
        foreach(var activity in AllActivities(Tree))activity.ObjectiveOwner?.Dispose();
        foreach(var activity in AllActivities(Tree))activity.Strategy.Dispose();
        foreach(var chooser in AllActivities(Tree).SelectMany(a=>new[]{a.Chooser,a.InterludeChooser}).OfType<SelectionChooser>())chooser.Dispose();
        foreach(var activity in AllActivities(Tree))activity.SparkedOwner?.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeLookForFaceAndCubeBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativePickUpCubeBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeKnockOverCubesBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeBouncerBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeBringCubeToBeaconBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeCubeLiftWorkoutBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeDanceBehavior>())candidate.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeDevTurnInPlaceTestBehavior>())candidate.Dispose();
        Configurations.Dispose();
        Manipulation?.Dispose();
    }

    private sealed class ConfiguredActivity(Activity activity,EngineRobot robot,BehaviorManager manager,BehaviorContext context,Action waitForParent,Func<bool> waiting,Action retireUnavailable):IManagedActivity,IHostReadinessOwner
    {
        public void Update(double now){if(waiting())return;if(activity.SparkedOwner is {} spark)spark.Update();else _=activity.NativeUpdate20!(robot);}
        // No interlude is configured on the admitted MeetCozmo/PutDown routes.
        // 0x005B387C/38C2/005B3A6C: no cached normal winner; chooser runs each tick.
        public IBehavior? GetDesiredActiveBehavior(IBehavior? current,double now)
        {
            if(waiting())return current;
            if(activity.SparkedOwner is {} spark)
            {
                var next=spark.Desired(current,now,context);
                if(spark.InputUnavailable){retireUnavailable();return null;}
                if(next is null&&spark.State is 4 or 5)waitForParent();
                return next;
            }
            var decision=activity.Chooser?.GetDesiredActiveBehavior(current,manager.RunningDurationSec(now),context,now);
            if(decision?.InputUnavailable==true){SteppedBehavior.ReportMissing("M8-013 configured chooser input unavailable; prior behavior retained");return current;}
            var desired=decision?.Behavior;
            if(desired is NativeLookForFaceAndCubeBehavior look && !look.ExecutionAvailable)
            {
                SteppedBehavior.ReportMissing("PutDownDispatch LookForFaceAndCube actual input owner unavailable; prior execution retained");
                return current;
            }
            return desired;
        }
        public void BehaviorSwitched(IBehavior? desired,bool started,double now){}
        bool IHostReadinessOwner.ReadinessChecksEnabled=>activity.SparkedOwner?.Delegate is not null || activity.Id=="SparksWorkout";
        bool IHostReadinessOwner.EvaluateCandidateReadiness=>true;
        void IHostReadinessOwner.RetireUnavailable()=>retireUnavailable();
    }
}
