using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Three configured PlayAnimOnNeedsChange owners: inherited WithFace and single-trigger sequence.
internal sealed class NativeNeedsGetInBehavior : SteppedBehavior, IDisposable
{
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _m;
    private readonly NeedsManager _needs;
    private readonly NeedId _need;
    private readonly List<AnimationTrigger> _triggers=new();
    private readonly bool _valid;
    private long _generation;
    private int _retired;
    internal NeedId Need=>_need;
    internal IReadOnlyList<AnimationTrigger> Triggers=>_triggers;
    internal bool ParametersAvailable=>_valid;
    internal Action? BeforeBracketForTest;
    private bool Available => _valid && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} runtime && ReferenceEquals(runtime.Manipulation,_m) &&
        runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this) && ReferenceEquals(_needs,_robot.Engine.Needs) &&
        ReferenceEquals(runtime.AI.SevereNeeds,_needs.SevereNeeds) && ReferenceEquals(_m.World,_m.Robot.LocalizationWorld) &&
        _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null && _robot.MainPose.Node is not null;
    internal NativeNeedsGetInBehavior(BehaviorFactoryContext source,JsonElement config)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"PlayAnimOnNeedsChange")
    {
        _robot=source.NativeRobot!;_m=source.Manipulation!;_needs=_robot.Engine.Needs;
        try
        {
            if(!Enum.TryParse(config.GetProperty("need").GetString(),false,out _need) || !Enum.IsDefined(_need))return;
            if(config.TryGetProperty("animTriggers",out var triggers) && triggers.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null))return;
            if(triggers.ValueKind==JsonValueKind.Array)
                foreach(var item in triggers.EnumerateArray())
                {
                    string? name=item.ValueKind==JsonValueKind.String?item.GetString():null;
                    if(name=="Count")continue;
                    if(name is not null && Enum.TryParse<AnimationTrigger>(name,false,out var trigger) && Enum.IsDefined(trigger))_triggers.Add(trigger);
                    else {BehaviorClassNames.WriteCerrError(name??"","AnimationTrigger");_triggers.Add((AnimationTrigger)0);}
                }
            int loops=config.TryGetProperty("num_loops",out var count)?count.GetInt32():1;
            _valid=_triggers.Count<=1 && loops==1;
        }
        catch(Exception error) when(error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {ReportMissing("PlayAnimOnNeedsChange configured inputs unavailable: "+error.Message);}
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)
    {
        if(Volatile.Read(ref _retired)!=0 || Interlocked.Read(ref _generation)!=version || !ReferenceEquals(_robot.Engine.Robot,_robot))return false;
        if(!Available){ReportMissing("PlayAnimOnNeedsChange live original owner/inputs unavailable");return false;}
        return true;
    }
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==version||Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override int ResumeInternal()=>1;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available || !ReferenceEquals(context.Robot,_m.Robot) || !ReferenceEquals(context.Needs,_needs))
        {MarkReadinessUnavailable();ReportMissing("PlayAnimOnNeedsChange actual original owner/needs input unavailable");return false;}
        long generation=CaptureHostExecutionVersion();
        if(!IsRunnableBase(context) || !HostExecutionVersionCurrent(generation))return false;
        return IsRunnableInternal(context);
    }
    private bool ShouldGetIn(long generation)
    {
        int selected=_needs.SevereNeeds.Selected;
        BeforeBracketForTest?.Invoke();
        if(!HostExecutionVersionCurrent(generation))return false;
        bool critical=_needs.State.IsNeedAtBracket(_need,NeedBracketId.Critical);
        return HostExecutionVersionCurrent(generation) && selected!=(int)_need && critical;
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
        =>_triggers.Count!=0 && ShouldGetIn(CaptureHostExecutionVersion());
    protected override void OnStart()
    {
        if(!Available){MarkReadinessUnavailable();InitFailed=true;ReportMissing("PlayAnimOnNeedsChange actual original owner/needs input unavailable");return;}
        long generation=CaptureHostExecutionVersion();var face=new NativeLastFaceRunner(_m,_robot,0);
        if(!HostExecutionVersionCurrent(generation)){face.RetireNeverAdmittedTree();return;}
        StartActing(face,_=>StartAnimation(generation),()=>HostExecutionVersionCurrent(generation));
    }
    private void StartAnimation(long generation)
    {
        if(!HostExecutionVersionCurrent(generation))return;
        if(_triggers.Count!=1){ReportMissing("PlayAnimOnNeedsChange changed trigger sequence unavailable");return;}
        var action=new TriggerLiftSafeAnimationAction(_m.Robot,new(_triggers[0],1,true,0,60f,false));
        if(!HostExecutionVersionCurrent(generation)){action.RetireNeverAdmittedTree();return;}
        StartActing(action,_=>{},()=>HostExecutionVersionCurrent(generation)); // shipped listener tree is empty
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        long generation=Interlocked.Increment(ref _generation);
        if(Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) && Available && ShouldGetIn(generation))
            _needs.SevereNeeds.Set(_need);
    }
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);RetireActionCompletionOwner();}
}
