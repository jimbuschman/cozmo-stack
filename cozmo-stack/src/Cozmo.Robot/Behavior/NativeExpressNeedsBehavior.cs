using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Actual configured ExpressNeeds owner; standalone ExpressNeedsBehavior remains its public legacy API.
internal sealed class NativeExpressNeedsBehavior : SteppedBehavior, IDisposable
{
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _m;
    private readonly NeedsManager _needs;
    private readonly NeedId _need;
    private readonly NeedBracketId _bracket;
    private readonly Graph2d? _cooldown;
    private readonly List<AnimationTrigger> _triggers=new();
    private readonly bool _clear,_cares,_valid;
    private float _lastSuccess;
    private long _generation;
    private int _retired;
    internal float LastSuccessSec=>_lastSuccess;
    internal IReadOnlyList<AnimationTrigger> Triggers=>_triggers;
    internal NeedId Need=>_need;
    internal NeedBracketId Bracket=>_bracket;
    internal bool ParametersAvailable=>_valid;
    internal Action? BeforeOwnPredicateForTest;
    private bool Available => _valid && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} runtime && ReferenceEquals(runtime.Manipulation,_m) &&
        runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this) && ReferenceEquals(_needs,_robot.Engine.Needs) &&
        ReferenceEquals(runtime.AI.SevereNeeds,_needs.SevereNeeds) && ReferenceEquals(_m.World,_m.Robot.LocalizationWorld) &&
        _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null && _robot.MainPose.Node is not null;

    internal NativeExpressNeedsBehavior(BehaviorFactoryContext source,JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"ExpressNeeds")
    {
        _robot=source.NativeRobot!;_m=source.Manipulation!;_needs=_robot.Engine.Needs;
        try
        {
            if(!Enum.TryParse(config.GetProperty("need").GetString(),false,out _need) ||
               !Enum.TryParse(config.GetProperty("needBracket").GetString(),false,out _bracket) ||
               !Enum.IsDefined(_need) || !Enum.IsDefined(_bracket))return;
            _cooldown=config.TryGetProperty("cooldown",out var graph)?Graph2d.FromJson(graph):null;
            if(_cooldown is null)return;
            if(config.TryGetProperty("animTriggers",out var triggers) && triggers.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null))return;
            if(triggers.ValueKind==JsonValueKind.Array)
                foreach(var item in triggers.EnumerateArray())
                {
                    string? name=item.ValueKind==JsonValueKind.String?item.GetString():null;
                    if(name=="Count")continue;
                    if(name is not null && Enum.TryParse<AnimationTrigger>(name,false,out var trigger) && Enum.IsDefined(trigger))_triggers.Add(trigger);
                    else {BehaviorClassNames.WriteCerrError(name??"","AnimationTrigger");_triggers.Add((AnimationTrigger)0);}
                }
            if(config.TryGetProperty("shouldClearExpressedState",out var clear) && clear.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))return;
            if(config.TryGetProperty("caresAboutExpressedState",out var cares) && cares.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))return;
            _clear=clear.ValueKind==JsonValueKind.True;_cares=cares.ValueKind==JsonValueKind.True;
            _valid=true;
        }
        catch(Exception error) when(error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {ReportMissing("ExpressNeeds configured inputs unavailable: "+error.Message);}
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)
    {
        if(Volatile.Read(ref _retired)!=0 || Interlocked.Read(ref _generation)!=version || !ReferenceEquals(_robot.Engine.Robot,_robot))return false;
        if(!Available){ReportMissing("ExpressNeeds live original owner/inputs unavailable");return false;}
        return true;
    }
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==version||Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override int ResumeInternal()=>1;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available || !ReferenceEquals(context.Robot,_m.Robot) || !ReferenceEquals(context.Needs,_needs))
        {MarkReadinessUnavailable();ReportMissing("ExpressNeeds actual original owner/needs input unavailable");return false;}
        long generation=CaptureHostExecutionVersion();
        if(!IsRunnableBase(context) || !HostExecutionVersionCurrent(generation))return false;
        return IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        long generation=CaptureHostExecutionVersion();
        BeforeOwnPredicateForTest?.Invoke();
        if(!HostExecutionVersionCurrent(generation))return false;
        if(_cares && _bracket==NeedBracketId.Critical)
        {if(_needs.SevereNeeds.Selected!=(int)_need)return false;}
        else if(!_needs.State.IsNeedAtBracket(_need,_bracket))return false;
        if(!HostExecutionVersionCurrent(generation))return false;
        float cooldown=(float)_cooldown!.EvaluateY(_needs.State.GetNeedLevel(_need));
        return HostExecutionVersionCurrent(generation) && cooldown+_lastSuccess<_robot.Engine.Timer.SecondsF;
    }
    protected override void OnStart()
    {
        if(!Available){MarkReadinessUnavailable();InitFailed=true;ReportMissing("ExpressNeeds actual original owner/needs input unavailable");return;}
        long generation=CaptureHostExecutionVersion();
        var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(new NativeLastFaceRunner(_m,_robot,0),true,false);
        foreach(var trigger in _triggers)
        {
            var child=new TriggerAnimationAction(_m.Robot,new(trigger,1,true,0,60f,false));
            if(!HostExecutionVersionCurrent(generation)){child.RetireNeverAdmittedTree();sequence.RetireNeverAdmittedTree();return;}
            sequence.AddAction(child,false,false);
        }
        if(!HostExecutionVersionCurrent(generation)){sequence.RetireNeverAdmittedTree();return;}
        StartActing(sequence,result=>
        {
            if(HostExecutionVersionCurrent(generation) && (result.Result>>24)==0)_lastSuccess=_robot.Engine.Timer.SecondsF;
        },()=>HostExecutionVersionCurrent(generation));
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        if(_clear && ReferenceEquals(_robot.Engine.Robot,_robot) && Volatile.Read(ref _retired)==0)_needs.SevereNeeds.Clear();
    }
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);RetireActionCompletionOwner();}
}
