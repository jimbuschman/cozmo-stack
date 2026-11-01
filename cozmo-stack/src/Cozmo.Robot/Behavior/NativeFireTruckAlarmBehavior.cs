using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-109
// 005DAF88..005DB0E2: the engine hands the trick to the app; it creates no action.
internal sealed class NativeFireTruckAlarmBehavior : SteppedBehavior, IDisposable
{
    private readonly EngineRobot _robot;
    private long _generation;
    private int _retired;
    internal EngineRobot OriginalRobot => _robot;
    internal NativeFireTruckAlarmBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "FireTruckAlarm") => _robot=source.NativeRobot!;
    private bool Available => Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is { IsRetired:false } runtime && runtime.Bound.TryGetValue(Id,out var owner) && ReferenceEquals(owner,this);
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Available && Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Available && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Available && (Interlocked.Read(ref _generation)==version || Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available){MarkReadinessUnavailable();ReportMissing("FireTruckAlarm original Robot owner unavailable");return false;}
        long version=CaptureHostExecutionVersion();
        return IsRunnableBase(context) && HostExecutionVersionCurrent(version) && IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)=>!_robot.NativeDoingCSharpTrick;
    protected override void OnStart()
    {
        long version=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(version)){InitFailed=true;MarkReadinessUnavailable();return;}
        if(_robot.NativeDoingCSharpTrick){InitFailed=true;return;}
        Log("BehaviorFireTruckAlarm starting CSharp trick");
        if(!HostExecutionVersionCurrent(version))return;
        ReportMissing("FireTruckAlarm StopTrick ingress and app-supplied flag clear, objective and needs events are not recovered");
        if(!HostExecutionVersionCurrent(version))return;
        _robot.NativeDoingCSharpTrick=true;
        if(_robot.Engine.NeedsContextExternalInterfaceAvailable)
            _robot.Engine.DeliverNurture(0x8F,Array.Empty<byte>(),null);
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    // 005DB0CC: unit-only until the actual StopTrick ingress is recovered.
    internal void StopTrickForTest(){using var owner=EnterHostExecutionOwner(null);if(Available)_robot.NativeDoingCSharpTrick=false;}
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)==0)Interlocked.Increment(ref _generation);}
}
