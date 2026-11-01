using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// 005DB0E8..005DB4DC: configured OnConfigSeen. Unknown count history cannot be reconstructed.
internal sealed class NativeRespondToPyramidBaseBehavior : SteppedBehavior, IDisposable
{
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _m;
    private long _generation;
    private int _retired;
    private bool _historyAvailable=true;
    private int _savedCount;
    private float _previous,_growth;
    internal bool HistoryAvailable=>_historyAvailable;
    internal (float Previous,float Growth,int Count) History=>(_previous,_growth,_savedCount);
    // Explicit source-state unit input; never a production history-recovery route.
    internal void SeedHistoryForTest(float previous,float growth,int count)
    {if(!_historyAvailable)throw new InvalidOperationException("Unknown history cannot be restored");_previous=previous;_growth=growth;_savedCount=count;}
    internal NativeRespondToPyramidBaseBehavior(BehaviorFactoryContext source,JsonElement config)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"OnConfigSeen")
    {_robot=source.NativeRobot!;_m=source.Manipulation!;}
    private bool Available=>Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {IsRetired:false} runtime && ReferenceEquals(runtime.Manipulation,_m) &&
        runtime.Bound.TryGetValue(Id,out var owner) && ReferenceEquals(owner,this);
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Available && Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Available && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Available && Interlocked.Read(ref _generation)==version;
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override int ResumeInternal()=>0;
    private bool Unavailable(string reason)
    {_historyAvailable=false;MarkReadinessUnavailable();ReportMissing("M12-049 RespondToPyramidBase count/history unavailable: "+reason);return false;}
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available)return Unavailable("original owner unavailable");
        long generation=CaptureHostExecutionVersion();
        var common=EvaluateHostOperation(()=>IsRunnableBase(context));
        if(!common.Available || !HostExecutionVersionCurrent(generation))return Unavailable("common readiness unavailable or owner changed");
        if(!common.Value)return false;
        return IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if(!_historyAvailable)return Unavailable("prior native evaluation was uncertain");
        long generation=CaptureHostExecutionVersion();float now=_robot.Engine.Timer.SecondsF;
        float elapsed=now-_previous;_previous=now;
        try
        {
            var runtime=_robot.NativeBehaviorRuntime??throw new NativeConfigurationUnavailable("runtime unavailable");
            int first=runtime.RequirePyramidSnapshot().Bases.Count;
            if(first>_savedCount)_growth=now;
            int second=runtime.RequirePyramidSnapshot().Bases.Count;
            if(!HostExecutionVersionCurrent(generation))return Unavailable("owner changed during count");
            _savedCount=second;
            return elapsed<BitConverter.UInt32BitsToSingle(0x409FFFEB) && MathF.Abs(_growth-now)<BitConverter.UInt32BitsToSingle(0x3727C5AC);
        }
        catch(NativeConfigurationUnavailable error){return Unavailable(error.Message);}
    }
    protected override void OnStart()
    {
        long generation=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(generation)){InitFailed=true;MarkReadinessUnavailable();return;}
        var action=new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)20,1,true,0,60f,false));
        if(!HostExecutionVersionCurrent(generation)){action.RetireNeverAdmittedTree();return;}
        StartActing(action,null,()=>HostExecutionVersionCurrent(generation));
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);RetireActionCompletionOwner();}
}
