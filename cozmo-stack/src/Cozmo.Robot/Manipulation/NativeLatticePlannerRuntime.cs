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
    private readonly Func<LatticeCarryingState> _carried;
    private readonly SourceLogSink _source;
    private readonly Action<string> _missing;
    private readonly CozmoEngine? _engine;
    private StateC _start;
    private readonly List<Pose3d> _goals=new(); // impl+C0: own nodes, source parent shared_ptr retained
    private readonly List<(byte Id,StateC C)> _goalContext=new(); // impl+88: admitted RAW pairs, not rounded outputs
    private IEnumerable<uint>? _ignore;
    private LatticePlan? _total;
    private int _disposed;
    internal volatile bool F0,F1,F3;
    internal bool F2=>_backend.RunFlag;
    internal bool F4=>true; // Only ctor writes 1; the other goal loop is unreachable.
    internal bool F5;
    private volatile int _status;
    private uint _searchNum;
    private byte _chosenGoal;
    private RetainedSubscription? _runModeSubscription;
    internal event Action<int>? StatusPublished;
    public string Name=>"LatticePlanner";
    public bool SkipsSafetyCheck=>true;
    public int CheckPlanningStatus=>_status;
    internal LatticePlan? TotalPlan=>_total;
    internal LatticePlanner Backend=>_backend;
    // Explicit raw-state constructor for standalone test/tool fixtures.
    internal NativeLatticePlannerRuntime(LatticePlanner backend,BlockWorld world,Func<uint?> carried,SourceLogSink source,Action<string> missing,CozmoEngine? engine=null)
        : this(backend,world,() => LatticeCarryingState.FromRaw(carried()),source,missing,engine) { }
    internal NativeLatticePlannerRuntime(LatticePlanner backend,BlockWorld world,Func<LatticeCarryingState> carried,SourceLogSink source,Action<string> missing,CozmoEngine? engine=null)
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
            AssignGoalPoses(goals); // 0x004FD296..2A4, all full poses before validity/old-context clearing
            _backend.ImportBlockWorldObstaclesWithCarrying(_world,_carried,false); // 0x004FD2B0 precedes old actions/context clearing
            if(_total is {} old)_total=old with{Actions=Array.Empty<MotionPrimitive>(),Penalties=Array.Empty<float>()};
            _goalContext.Clear();
            for(int i=0;i<goals.Count;i++)
            {
                var raw=LatticePlanner.ToStateC(goals[i]); // 0x004FD348..382 reads incoming poses synchronously
                if(_backend.GoalIsValid(raw))_goalContext.Add((unchecked((byte)i),raw));
            }
            if(_goalContext.Count==0)return 0;
            return ComputeNewPathIfNeeded(start,true);
        }
        finally{if(held)Monitor.Exit(ContextMutex);}
    }
    // fidelity: M13-033, M11-084 -- 0x00501BC0/1C40/1C70/1C92/1CA8..BA.
    private void AssignGoalPoses(IReadOnlyList<Pose3d> incoming)
    {
        if(ReferenceEquals(incoming,_goals))return;
        if(incoming.Count>_goals.Capacity)
        {
            ReleaseGoalPoses(); // native deallocates old elements before new storage/copy
            _goals.Capacity=incoming.Count;
        }
        int common=Math.Min(_goals.Count,incoming.Count);
        for(int i=0;i<common;i++)_goals[i].Assign(incoming[i]);
        for(int i=common;i<incoming.Count;i++)_goals.Add(new Pose3d(incoming[i]));
        for(int i=_goals.Count-1;i>=incoming.Count;i--){_goals[i].Dispose();_goals.RemoveAt(i);}
    }
    private void ReleaseGoalPoses()
    {
        for(int i=_goals.Count-1;i>=0;i--)_goals[i].Dispose();
        _goals.Clear();
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
            var stateLog=LatticePlanner.ToStateC(start); // 0x004FEB82..AA, synchronous caller-side snapshot
            var selectedStart=stateLog;
            _backend.SetReplanFlag(force);
            (int Index,float Distance) nearest=(0,float.MaxValue);
            // fidelity: M13-033 force path 0x004FEBD2 -> 0x004FECDA..0x004FECF6: the total takes the zeroed start
            // (0x004FEB6C..0x004FEB7C) and the empty action vector; PlanIsSafe and OldPlanUnsafe are skipped (B-M13-6).
            if(force)_total=new LatticePlan(default,Array.Empty<MotionPrimitive>(),0,0);
            else
            {
                _backend.ImportBlockWorldObstaclesWithCarrying(_world,_carried,true,_ignore);
                var closest=NativePlanDependencies.FindClosest(_backend.Env.Primitives,_total,stateLog);
                nearest=closest;
                if(closest.Distance>=20f)
                {
                    Info("LatticePlanner.GetPlan.ForcePlan",FormattableString.Invariant($"Current state is {closest.Distance:F6} away from the plan (planIdx {closest.Index}), forcing replan from scratch"));_total=null;
                }
                var retained=_total ?? new LatticePlan(default,Array.Empty<MotionPrimitive>(),0,0);
                var prefix=new LatticePlan(retained.Start,Array.Empty<MotionPrimitive>(),0,0);StateC endpoint=default;
                // StartPlanning calls PlanIsSafe at 0x004FF424 only when force is false.
                if(NativePlanDependencies.PlanIsSafe(retained,nearest.Index,40f,_backend.Env.Primitives,NativeApplyAction,ref prefix,ref endpoint,_missing))return 2;
                Info("LatticePlanner.GetPlan.OldPlanUnsafe",$"old plan unsafe! Will replan, starting from {nearest.Index}, keeping {prefix.Actions.Count} actions from oldPlan.");
                _total=prefix;if(prefix.Actions.Count!=0)selectedStart=endpoint;
            }
            _start=selectedStart; // 0x004FEED6/DA: persist scalar selected start before validation/signal
            _source(EngineLogLevel.Info,"Unnamed","LatticePlanner","currentRobotState:("+LibcxxPrintf.FormatFloat(stateLog.X)+","+LibcxxPrintf.FormatFloat(stateLog.Y)+","+LibcxxPrintf.FormatFloat(stateLog.Theta)+")");
            if(!_backend.CheckContextStart(_start,out _))
            {Info("LatticePlanner.ReplanIfNeeded.InvalidStart","could not set start");return 0;}
            if(!_backend.CheckContextGoalsWithIds(_goalContext,new(),new()))
            {Info("LatticePlanner.ReplanIfNeeded.InvalidGoals","Goals may have moved into collision");return 0;}
            _backend.ImportBlockWorldObstaclesWithCarrying(_world,_carried,false,_ignore);
            _missing("M13-033 Replanning State/plan text, PrepareForPlanning and conditional EnvDump recipient at 0x004FF052..0x004FF30A remain incomplete");
            _searchNum=unchecked(_searchNum+1);Info("LatticePlannerImpl.GetPlan",$"searchNum: {unchecked((int)_searchNum)}");
            _backend.SetRunFlag(true);
            if(!F5){F0=true;Publish(1);_wake.Set();}
            else {Info("LatticePlanner.RunSynchronous.DoPlanning","Do planning now in StartPlanning...");DoPlanning();}
            return 1;
        }
        finally{Monitor.Exit(ContextMutex);}
    }
    internal (LatticeState State,float Penalty) NativeApplyAction(LatticeState state,MotionPrimitive primitive,bool check)=>_backend.Env.ApplyAction(state,primitive.ActionIndex,check);
    private void DoPlanning()
    {
        using var poseLogs=Pose3d.UseSourceLog(_source,_engine is null?null:_engine.Das.ForceFlushNow);
        Publish(1);
        var result=_backend.DoPlanningContext(_start,_goalContext,out var plan);
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
                _missing("M13-033 LatticePlanner.PlanMismatch sError stream 0x005003D0 and its error side effects remain unported");
                _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","totalPlan_:");_missing("M13-033 PrintPlan(total) 0x0050048E -> 0x00855E7C output remains unbuilt");
                _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","new plan:");_missing("M13-033 PrintPlan(new) 0x005004D8 -> 0x00855E7C output remains unbuilt");
                Publish(0);
            }
        }
        _chosenGoal=unchecked((byte)_backend.ChosenGoalId); // 0x005004F0: success-tail snapshot at +0x100.
        _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","old plan");_missing("M13-033 PrintPlan 0x00500532 -> 0x00855E7C output remains unbuilt");
        _total=NativePlanDependencies.Append(_total ?? new LatticePlan(default,Array.Empty<MotionPrimitive>(),0,0),plan);
        _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","new plan");_missing("M13-033 PrintPlan 0x0050058A -> 0x00855E7C output remains unbuilt");
        Publish(2);
    }
    private void Worker()
    {
        if (F1) return; // OV09 W1: an exit request observed at thread entry precedes pending work.
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
        // Native writes goal after the status 0/1 exits (0x004FFB8C); C# needs the out set first, and Read ignores it on false.
        path=Array.Empty<PathSegment>();goal=_chosenGoal;
        if(_status is 0 or 1 || _total is null)return false;
        var nearest=NativePlanDependencies.FindClosest(_backend.Env.Primitives,_total,LatticePlanner.ToStateC(start));
        Info("LatticePlanner.GetCompletePath.Offset",FormattableString.Invariant($"Robot is {nearest.Distance:F6} from the plan, at index {nearest.Index}"));
        if(nearest.Distance>=20f){_missing("M13-033 RobotPositionError event/format at 0x004FFDCC..0x004FFE2A remains unported");_total=null;Publish(0);return false;}
        _source(EngineLogLevel.Debug,"Planner","LatticePlannerImpl","total path:");
        _missing("M13-033 Path::PrintPath call 0x004FFC76 output remains unbuilt");
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
            float desired=(float)_goals[_chosenGoal].AngleAroundZ,delta=EngineF.RadiansMinus(desired,endAngle),speed=delta>0?2f:-2f;
            _missing("M13-034 final turn uses the retained ToPath candidate's lattice endpoint; native GetEndPose cached heading/primitive paths remain unbuilt (0x0085BF5C)");
            Info("LatticePlanner.ReplanIfNeeded.FinalAngleCorrection",FormattableString.Invariant($"LatticePlanner: Final angle off by {delta:F6} rad. DesiredAng = {desired:F6}, endAngle = {endAngle:F6}, rotSpeed = {speed:F6}. Adding point turn."));
            NativePathBuffer.Append(raw,new PathSegment.PointTurn(_backend.Env.Primitives.ResolutionMm*endpoint.X,_backend.Env.Primitives.ResolutionMm*endpoint.Y,desired,EngineF.Bits(0x3DB2B8C2),speed,10,10,true),_missing,"AppendPointTurn");
        }
        path=NativePathBuffer.ApplyKnownProfile(raw,profile,_missing,_source);return true;
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref _disposed,1)!=0)return;F1=true;_wake.Set();
        _source(EngineLogLevel.Debug,"Planner","LatticePlanner.DestroyThread.Join","");_worker.Join();
        _source(EngineLogLevel.Debug,"Planner","LatticePlanner.DestroyThread.Joined","");
        ReleaseGoalPoses();_goalContext.Clear(); // 0x00501954 join precedes C0 destruction0x005019D8/DC
        _runModeSubscription?.Dispose();_runModeSubscription=null;_wake.Dispose();_backend.NativeRuntime=null;
    }
}
