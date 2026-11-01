using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// 005DAE98..005DAF82: actual configured owner; reward production remains independently unavailable.
internal sealed class NativeEarnedSparksBehavior : SteppedBehavior,IDisposable
{
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _m;
    private readonly NeedsManager _needs;
    private long _generation;
    private int _retired;
    internal NativeEarnedSparksBehavior(BehaviorFactoryContext source,JsonElement config)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"EarnedSparks")
    {_robot=source.NativeRobot!;_m=source.Manipulation!;_needs=_robot.Engine.Needs;}
    private bool Available=>Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        ReferenceEquals(_robot.Engine.Needs,_needs) && _robot.NativeBehaviorRuntime is {IsRetired:false} runtime &&
        ReferenceEquals(runtime.Manipulation,_m) && runtime.Bound.TryGetValue(Id,out var owner) && ReferenceEquals(owner,this);
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long generation)=>Interlocked.Read(ref _generation)==generation && Available;
    protected override bool HostStopContinuationCurrent(long generation)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(generation+1) && ReferenceEquals(_robot.Engine.Robot,_robot);
    protected override bool HostResumeContinuationCurrent(long generation)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==generation || Interlocked.Read(ref _generation)==unchecked(generation+1)) && ReferenceEquals(_robot.Engine.Robot,_robot);
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override int ResumeInternal()=>1;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available || !ReferenceEquals(context.Needs,_needs)) {MarkReadinessUnavailable();ReportMissing("EarnedSparks original Robot/Needs owner unavailable");return false;}
        long generation=CaptureHostExecutionVersion();
        if(!IsRunnableBase(context) || !HostExecutionVersionCurrent(generation))return false;
        return IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        long generation=CaptureHostExecutionVersion();long epoch=_needs.NativePendingHistoryEpoch;
        if(!_needs.TryGetNativePending(out bool pending)) {MarkReadinessUnavailable();ReportMissing("M15-071/M15-093/M15-094 EarnedSparks native pending producer unavailable");return false;}
        if(!HostExecutionVersionCurrent(generation) || epoch!=_needs.NativePendingHistoryEpoch)
        {MarkReadinessUnavailable();ReportMissing("M15-071 EarnedSparks pending owner/history changed during readiness");return false;}
        return pending;
    }
    protected override void OnStart()
    {
        long generation=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(generation))
        {InitFailed=true;MarkReadinessUnavailable();ReportMissing("EarnedSparks original owner unavailable at Init");return;}
        var action=new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0xA4,1,true,0,60f,false));
        if(!HostExecutionVersionCurrent(generation)){action.RetireNeverAdmittedTree();return;}
        StartActing(action,null,()=>HostExecutionVersionCurrent(generation));
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        if(!Available)return;
        if(!_needs.TryGetNativePending(out bool pending)) {ReportMissing("M15-071 EarnedSparks Stop native pending input unavailable");return;}
        if(pending)_needs.CommunicateNativeReward(_robot);
    }
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);RetireActionCompletionOwner();}
}
