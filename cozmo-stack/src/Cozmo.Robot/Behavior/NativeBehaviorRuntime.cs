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
    internal IReadOnlyDictionary<string,IBehavior> Bound {get;}
    internal IReadOnlyList<Activity> Tree {get;}
    internal FreeplayDataTracker Tracker {get;}
    private Activity? _selected;
    private bool _retired;
    private readonly Action<bool> _offTreadsPause,_chargerPause;
    private readonly VisionSystem? _ownedVision;
    private readonly OverheadEdgesDetector? _ownedOverhead;

    internal NativeBehaviorRuntime(CozmoRobot facade,EngineRobot robot,BehaviorManager manager,
        BehaviorContext context,VisionSystem? vision)
    {
        _facade=facade;_robot=robot;_manager=manager;Context=context;
        Context.Mood=robot.MoodOwner;
        AI=new(()=>unchecked((int)robot.CurrentOriginId),facade.Engine.EmitStackLog,
            (l,c,k,t)=>facade.Engine.EmitSourceLog(l,c,k,t),facade.Engine.Needs.SevereNeeds);
        Context.AI=AI;
        Tracker=new(()=>unchecked((ulong)facade.Engine.Timer.Nanoseconds))
        {SourceLog=(l,c,k,t)=>facade.Engine.EmitSourceLog(l,c,k,t)};
        AI.Tracker=Tracker;
        AI.BindDestructionRecipient(0x2C,Tracker.DestroyStorage);
        robot.Lifetime.Bind(-2,_=>Tracker.ForceUpdate()); // 0x0051111A, before AbortAll
        _offTreadsPause=paused=>Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads,paused);
        _chargerPause=paused=>Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OnCharger,paused);
        facade.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads=_offTreadsPause;
        facade.Sensors.OnChargerPlatformChangedInternal+=_chargerPause;
        Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,true);
        Configurations=new(facade.LocalizationWorld,robot);
        facade.LocalizationWorld.NativeConfigurations=Configurations;
        if(vision is not null)
        {
            Manipulation=new(facade,vision,robot){ClockSec=()=>facade.Engine.Timer.SecondsF};
            if(vision.OverheadEdges is null)
            {
                _ownedVision=vision;_ownedOverhead=new OverheadEdgesDetector();vision.OverheadEdges=_ownedOverhead;
            }
        }
        var factory=new BehaviorFactoryContext{Vision=vision,Manipulation=Manipulation,NativeRobot=robot,
            SourceLog=(l,c,k,t)=>facade.Engine.EmitSourceLog(l,c,k,t),Log=facade.Engine.EmitStackLog};
        var container=new BehaviorContainer(new BehaviorFactory(),factory,
            facade.Engine.HostBehaviorMap??new Dictionary<BehaviorID,System.Text.Json.JsonElement>());
        var bound=new Dictionary<string,IBehavior>(StringComparer.Ordinal);
        foreach(var behavior in container.Behaviors.Values)
        {
            bound.Add(behavior.Id,behavior);
            if(behavior is SteppedBehavior stepped)stepped.Clock=()=>facade.Engine.Timer.SecondsF*1000.0;
            if(manager.Find(behavior.Id) is null)manager.Add(behavior);
        }
        Bound=bound;
        string? resources=facade.Engine.Options.ResourcesPath;
        if(resources is not null && Directory.Exists(Path.Combine(resources,"config","engine","behaviorSystem","activities"))
            &&File.Exists(Path.Combine(resources,"config","engine","behaviorSystem","activities_config.json")))
        {
            Tree=ActivityTreeLoader.LoadResources(resources,Bound,manager.Penalty,robot);
            foreach(var activity in AllActivities(Tree))activity.Analyzer=AI.Analyzer;
            robot.NativeActivityRecipientsConstructed=true;
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
        var selected=AllActivities(Tree).FirstOrDefault(a=>a.Id==activityId)
            ??throw new ArgumentException("No configured activity named "+activityId,nameof(activityId));
        if(selected.Id!="PutDownDispatch" || selected.Type!="BehaviorsOnly" || selected.NativeUpdate20 is null || selected.InterludeChooser is not null)
        {
            SteppedBehavior.ReportMissing("Native activity admission "+activityId+": specialized lifecycle/interlude is not bound; prior activity retained (M15-061 for Feeding)");
            return;
        }
        if(selected.Chooser?.BehaviorIds.Any(id=>Bound.TryGetValue(id,out var candidate)
            &&candidate is NativeLookForFaceAndCubeBehavior look&&!look.HasCheckedObjective41)==true)
        {
            SteppedBehavior.ReportMissing("Native PutDownDispatch admission: LookForFaceAndCube explicit objective input lacks checked invalid41 contract; prior activity retained before selection/motion");
            return;
        }
        float now=_facade.Engine.Timer.SecondsF;
        _manager.Stop(BehaviorStopReason.Interrupted,now);
        _selected?.OnDeselected(now,_facade.Engine.EmitStackLog);
        _selected=selected;
        selected.OnSelected(now,_facade.Engine.EmitStackLog);
        Tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl,selected.Id!="Freeplay");
        // Spark is written only by the unbuilt specialized Freeplay selection path 0x005AE4E2..F2.
        _manager.Activity=new ConfiguredActivity(selected,_robot,_manager,Context);
    }

    internal FreeplayDecision Observe()=>new(_facade.Engine.Timer.SecondsF,_selected?.Id,_manager.Current?.Id,"native engine observation");
    private FreeplaySystem? _observer;
    internal FreeplaySystem FreeplayObserver=>_observer??=new FreeplaySystem(_manager,Context,
        Tree.FirstOrDefault(a=>a.Id=="Freeplay")??throw new InvalidOperationException("Configured Freeplay resources unavailable"),
        Bound,new FreeplayInputs{Needs=_facade.Engine.Needs,Mood=_robot.MoodOwner},false){ObserveOnly=Observe};
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
        Context.Map=_facade.LocalizationWorld.Maps.Current;
        _manager.Update(capturedNow*1000.0,capturedNow);
    }

    public void Dispose()
    {
        if(_retired)return;_retired=true;
        if(ReferenceEquals(_facade.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads,_offTreadsPause))
            _facade.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads=null;
        _facade.Sensors.OnChargerPlatformChangedInternal-=_chargerPause;
        if(_ownedVision is {} vision&&ReferenceEquals(vision.OverheadEdges,_ownedOverhead))vision.OverheadEdges=null;
        // The manager is stopped/disposed by its actual lifetime owner first.
        _observer?.DetachHostHandle();
        foreach(var activity in AllActivities(Tree))activity.ObjectiveOwner?.Dispose();
        foreach(var candidate in Bound.Values.OfType<NativeLookForFaceAndCubeBehavior>())candidate.Dispose();
        Configurations.Dispose();
        Manipulation?.Dispose();
    }

    private sealed class ConfiguredActivity(Activity activity,EngineRobot robot,BehaviorManager manager,BehaviorContext context):IManagedActivity
    {
        public void Update(double now)=>_=activity.NativeUpdate20!(robot);
        // No interlude is configured on the admitted MeetCozmo/PutDown routes.
        // 0x005B387C/38C2/005B3A6C: no cached normal winner; chooser runs each tick.
        public IBehavior? GetDesiredActiveBehavior(IBehavior? current,double now)
        {
            var desired=activity.Chooser?.GetDesiredActiveBehavior(current,manager.RunningDurationSec(now),context,now).Behavior;
            if(desired is NativeLookForFaceAndCubeBehavior)
            {
                SteppedBehavior.ReportMissing("PutDownDispatch LookForFaceAndCube selected Init/Resume/action body unavailable; scored candidate retained, prior execution retained");
                return current;
            }
            return desired;
        }
        public void BehaviorSwitched(IBehavior? desired,bool started,double now){}
    }
}
