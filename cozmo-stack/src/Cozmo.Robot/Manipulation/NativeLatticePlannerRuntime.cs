using Cozmo.Robot.Vision;
using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-033, M13-034, M13-018
// Native dispatch/worker owns the production backend. PlanTo remains an explicit synchronous tool API.
internal sealed class NativeLatticePlannerRuntime : INativePathPlanner,IDisposable
{
    internal readonly object ContextMutex=new();
    private readonly AutoResetEvent _wake=new(false);
    private readonly Thread _worker;
    private readonly LatticePlanner _backend;
    private readonly BlockWorld _world;
    private readonly Func<uint?> _carried;
    private readonly SourceLogSink _source;
    private readonly Action<string> _missing;
    private readonly CozmoEngine? _engine;
    private Pose3d _start;
    private IReadOnlyList<Pose3d> _goals=Array.Empty<Pose3d>();
    private IEnumerable<uint>? _ignore;
    private LatticePlan? _total;
    private bool _disposed;
    internal volatile bool F0,F1,F3;
    internal bool F2=>_backend.RunFlag;
    internal bool F4=>true; // Only ctor writes 1; the other goal loop is unreachable.
    internal bool F5;
    private volatile int _status;
    private uint _searchNum;
    private RetainedSubscription? _runModeSubscription;
    internal event Action<int>? StatusPublished;
    public string Name=>"LatticePlanner";
    public bool SkipsSafetyCheck=>true;
    public int CheckPlanningStatus=>_status;
    internal LatticePlan? TotalPlan=>_total;
    internal LatticePlanner Backend=>_backend;
    internal NativeLatticePlannerRuntime(LatticePlanner backend,BlockWorld world,Func<uint?> carried,SourceLogSink source,Action<string> missing,CozmoEngine? engine=null)
    {
        _engine=engine;_backend=backend;_world=world;_carried=carried;_source=source;_missing=missing;
        backend.NativeRuntime=this;backend.SetRunFlag(true);
        _runModeSubscription=engine?.AnimationMessages.SubscribeMessage(0x7B,false,_=>missing("M13-033 PlannerRunMode 0x7B callback argument/enum decoding is unported; F5 remains its checked asynchronous default"));
        _worker=new Thread(Worker){IsBackground=true,Name="cozmo-lattice-planner"};_worker.Start();
    }
    private void Publish(int value){_status=value;StatusPublished?.Invoke(value);}
    private void Info(string key,string text)=>_source(EngineLogLevel.Info,"Planner",key,text);
    public int ComputePath(Pose3d start,IReadOnlyList<Pose3d> goals)
    {
        bool held=Monitor.TryEnter(ContextMutex);
        if(!held)
        {
            if(F0 || F3){_source(EngineLogLevel.Warning,null,"LatticePlanner.ComputePath.AlreadyRunning",$"Tried to compute a new path, but the planner is already running! (timeToPlan {(F0?1:0)}, running {(F3?1:0)})");return 0;}
            Monitor.Enter(ContextMutex);held=true;
        }
        try
        {
            if(F0){_source(EngineLogLevel.Warning,null,"LatticePlanner.ComputePath.PreviousPlanRequested","timeToPlan already set, so another compute path call is already waiting to signal the planner");return 0;}
            _goals=goals.ToArray();
            _backend.Env.ImportBlockWorldObstacles(_world,_carried(),false);
            if(!_backend.CheckContextGoals(_goals.Select(LatticePlanner.ToStateC).ToArray(),new(),new()))return 0;
            return ComputeNewPathIfNeeded(start,true);
        }
        finally{if(held)Monitor.Exit(ContextMutex);}
    }
    internal void SetIgnore(IEnumerable<uint>? ignore)=>_ignore=ignore?.ToArray();
    public int ComputeNewPathIfNeeded(Pose3d start,bool force)
    {
        if(!Monitor.TryEnter(ContextMutex))return 2;
        try{return StartPlanning(start,force);}finally{Monitor.Exit(ContextMutex);}
    }
    internal int StartPlanning(Pose3d start,bool force)
    {
        if(!Monitor.TryEnter(ContextMutex))
        {
            _source(EngineLogLevel.Error,null,"LatticePlanner.StartPlanning.InternalThreadingError","Somehow failed to get mutex inside StartPlanning, but we should already have it at this point");EngineErrorState.StoreAndMaybeBreak();return 0;
        }
        try
        {
            _start=start;
            _backend.SetReplanFlag(force);
            if(force)_total=null;
            else
            {
                _backend.Env.ImportBlockWorldObstacles(_world,_carried(),true,_ignore);
                var closest=NativePlanDependencies.FindClosest(_backend.Env.Primitives,_total,LatticePlanner.ToStateC(start));
                if(closest.Distance>=20f)
                {
                    Info("LatticePlanner.GetPlan.ForcePlan",FormattableString.Invariant($"Current state is {closest.Distance:F6} away from the plan (planIdx {closest.Index}), forcing replan from scratch"));_total=null;
                }
                if(_total is {} old)
                {
                    if(closest.Index<old.Actions.Count){_missing("M13-034 ApplyAction(check=true) 0x00850478..0x0085060C lacks a checked collision body; reuse item stopped visibly, old plan retained, no dispatch");return 2;}
                    // Empty remainder does not call the unavailable collision body.
                    var prefix=new LatticePlan(old.Start,Array.Empty<MotionPrimitive>(),0,0);StateC endpoint=default;
                    if(NativePlanDependencies.PlanIsSafe(old,closest.Index,40f,_backend.Env.Primitives,NativeApplyAction,ref prefix,ref endpoint,_missing))return 2;
                    Info("LatticePlanner.GetPlan.OldPlanUnsafe",$"old plan unsafe! Will replan, starting from {closest.Index}, keeping {prefix.Actions.Count} actions from oldPlan.");
                    _total=prefix;if(prefix.Actions.Count!=0)_start=new Pose3d(Mat3.AboutZ(endpoint.Theta),new Vec3(endpoint.X,endpoint.Y,0));
                }
            }
            _missing("M13-033 currentRobotState State_c stream formatting at 0x004FEE3C -> 0x0084FD18 remains unbuilt; no guessed engine payload emitted");
            if(!_backend.CheckContextStart(LatticePlanner.ToStateC(_start),out _))
            {Info("LatticePlanner.ReplanIfNeeded.InvalidStart","could not set start");return 0;}
            if(!_backend.CheckContextGoals(_goals.Select(LatticePlanner.ToStateC).ToArray(),new(),new()))
            {Info("LatticePlanner.ReplanIfNeeded.InvalidGoals","Goals may have moved into collision");return 0;}
            _backend.Env.ImportBlockWorldObstacles(_world,_carried(),false,_ignore);
            _missing("M13-033 Replanning State/plan text, PrepareForPlanning and conditional EnvDump recipient at 0x004FF052..0x004FF30A remain incomplete");
            _searchNum=unchecked(_searchNum+1);Info("LatticePlannerImpl.GetPlan",$"searchNum: {unchecked((int)_searchNum)}");
            _backend.SetRunFlag(true);
            if(!F5){F0=true;Publish(1);_wake.Set();}
            else {Info("LatticePlanner.RunSynchronous.DoPlanning","Do planning now in StartPlanning...");DoPlanning();}
            return 1;
        }
        finally{Monitor.Exit(ContextMutex);}
    }
    private (LatticeState State,float Penalty) NativeApplyAction(LatticeState state,MotionPrimitive primitive,bool check)
    {
        var buckets=_backend.Env.Primitives.ByAngle;
        if(state.Theta>=buckets.Count || primitive.ActionIndex<0 || primitive.ActionIndex>=buckets[state.Theta].Count)return(state,0); // invalid lookup leaves StateID untouched, 0x008505BC..0x008505E6
        if(check)
        {
            _missing("M13-034 ApplyAction 0x00850478..0x0085060C is unread; stopping this safety/reuse item rather than substituting the different SuccessorIterator penalty");
            return (state,float.NaN); // Not dispatched: StartPlanning stops this item before the checking callback.
        }
        return (NativePlanDependencies.Advance(state,primitive),0);
    }
    private void DoPlanning()
    {
        Publish(1);
        var result=_backend.DoPlanning(LatticePlanner.ToStateC(_start),_goals.Select(LatticePlanner.ToStateC).ToArray(),out var plan);
        string eventName=result==LatticePlanner.PlanningResult.Failure?"robot.lattice_planner_failure":"robot.lattice_planner_success";
        string eventText=$"{_backend.LastNumExpansions}:{_backend.LastNumConsiderations}";
        if(_engine is {} engine)engine.EmitSourceLog(EngineLogLevel.Event,null,eventName,eventText,new Dictionary<string,string>{{"$data",_backend.LastPlanningElapsedMs.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        else _source(EngineLogLevel.Event,null,eventName,eventText);
        if(result==LatticePlanner.PlanningResult.Failure){Publish(0);return;}
        if(plan!.Actions.Count==0){Publish(3);return;}
        if(_total is {} prefix && prefix.Actions.Count!=0)
        {
            var end=prefix.States().Last();
            if(end!=plan.Start)
            {
                _missing("M13-033 LatticePlanner.PlanMismatch 0x005003D0 and old/new-plan streaming text are unported; mismatch transient status is retained");Publish(0);
            }
            _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","old plan");_missing("M13-033 PrintPlan 0x0050050C -> 0x00855E7C output remains unbuilt");
            _total=NativePlanDependencies.Append(prefix,plan);
            _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","new plan");_missing("M13-033 PrintPlan 0x00500562 -> 0x00855E7C output remains unbuilt");
        }
        else _total=plan;
        Publish(2);
    }
    private void Worker()
    {
        while(true)
        {
            lock(ContextMutex)
            {
                while(!F0 && !F1)
                {
                    Monitor.Exit(ContextMutex);try{_wake.WaitOne();}finally{Monitor.Enter(ContextMutex);}
                }
                if(F0){F3=true;F0=false;try{DoPlanning();}finally{F3=false;}}
            }
            if(F1)return;
        }
    }
    public void StopPlanning(){if(_worker is not null && F3)_backend.SetRunFlag(false);}
    public bool GetCompletePath(Pose3d start,PathMotionProfile profile,out IReadOnlyList<PathSegment> path,out byte goal)
    {
        path=Array.Empty<PathSegment>();goal=unchecked((byte)_backend.ChosenGoalId);
        if(_status is 0 or 1 || _total is null)return false;
        var nearest=NativePlanDependencies.FindClosest(_backend.Env.Primitives,_total,LatticePlanner.ToStateC(start));
        Info("LatticePlanner.GetCompletePath.Offset",FormattableString.Invariant($"Robot is {nearest.Distance:F6} from the plan, at index {nearest.Index}"));
        if(nearest.Distance>=20f){_missing("M13-033 RobotPositionError event/format at 0x004FFDCC..0x004FFE2A remains unported");_total=null;Publish(0);return false;}
        _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","total path:");
        _missing("M13-033 PrintPlan 0x004FFC4A -> 0x00855E7C output remains unbuilt");
        _missing("M13-034 AppendToPath cached primitive paths/MotionPrimitive.Create and RelativePathTurn.ExtendPath are unbuilt; existing ToPath geometry remains a labelled candidate, not the native AppendToPath implementation");
        var remaining=_total with{Start=_total.States().ElementAt(nearest.Index),Actions=_total.Actions.Skip(nearest.Index).ToArray(),Penalties=_total.Penalties.Skip(nearest.Index).ToArray()};
        var raw=_backend.ToPath(remaining,null,profile).ToList();
        bool hadSegments=raw.Count!=0;
        float? onlyTurnHeading=raw.FirstOrDefault() is PathSegment.PointTurn first?first.TargetAngleRad:null;
        while(raw.Count>0 && raw[^1] is PathSegment.PointTurn)raw.RemoveAt(raw.Count-1);
        if(hadSegments)
        {
            // Preserve the existing candidate producer's end-state input until cached native segments/GetEndPose are built.
            int endpointIndex=remaining.Actions.Count;
            while(endpointIndex>0 && remaining.Actions[endpointIndex-1].EndX==0 && remaining.Actions[endpointIndex-1].EndY==0)endpointIndex--;
            var endpoint=remaining.States().ElementAt(endpointIndex);float endAngle=_backend.Env.Primitives.Angles[endpoint.Theta];
            if(raw.Count==0 && onlyTurnHeading is {} cachedTurn)endAngle=cachedTurn;
            float desired=(float)_goals[_backend.ChosenGoalId].AngleAroundZ,delta=EngineF.RadiansMinus(desired,endAngle),speed=delta>0?2f:-2f;
            _missing("M13-034 final turn uses the retained ToPath candidate's lattice endpoint; native GetEndPose cached heading/primitive paths remain unbuilt (0x0085BF5C)");
            Info("LatticePlanner.ReplanIfNeeded.FinalAngleCorrection",FormattableString.Invariant($"LatticePlanner: Final angle off by {delta:F6} rad. DesiredAng = {desired:F6}, endAngle = {endAngle:F6}, rotSpeed = {speed:F6}. Adding point turn."));
            NativePathBuffer.Append(raw,new PathSegment.PointTurn(_backend.Env.Primitives.ResolutionMm*endpoint.X,_backend.Env.Primitives.ResolutionMm*endpoint.Y,desired,EngineF.Bits(0x3DB2B8C2),speed,10,10,true),_missing,"AppendPointTurn");
        }
        path=NativePathBuffer.ApplyKnownProfile(raw,profile,_missing,_source);return true;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;F1=true;_wake.Set();
        _source(EngineLogLevel.Debug,"Planner","LatticePlanner.DestroyThread.Join","");_worker.Join();
        _source(EngineLogLevel.Debug,"Planner","LatticePlanner.DestroyThread.Joined","");_wake.Dispose();_runModeSubscription?.Dispose();_runModeSubscription=null;_backend.NativeRuntime=null;
    }
}
