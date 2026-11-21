using System.Globalization;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Original Robot ownership and host reentrancy guards; these do not replace native action results.
internal sealed class PhysicalReactionOwner(ManipulationSystem manipulation,EngineRobot robot)
{
    internal ManipulationSystem M {get;}=manipulation;
    internal EngineRobot Robot {get;}=robot;
    private long _version;
    private bool _retired;
    internal long Version=>Interlocked.Read(ref _version);
    internal void Advance()=>Interlocked.Increment(ref _version);
    internal bool Current=>!_retired&&!Robot.Lifetime.Destroying && ReferenceEquals(Robot.Engine.Robot,Robot);
    internal void Retire(){_retired=true;Advance();}
    internal bool CanRun=>Current && Robot.NativeBehaviorRuntime?.IsRetired!=true;
    internal bool CurrentAt(long version)=>Current && Version==version;
    internal TriggerAnimationAction Animation(AnimationTrigger trigger,int loops=1) =>
        Prepare(new TriggerAnimationAction(M.Robot,new(trigger,loops,true,0,60f,false)));
    internal TriggerAnimationAction Prepare(TriggerAnimationAction action)
    {action.UseBoundedOrdinaryAuthoredAudio(Robot);return action;}
    internal bool Start(SteppedBehavior behavior,ActionRunner action,Action<RobotCompletedAction>? completion,long version)
    {
        if(!CurrentAt(version)||!CanRun||!behavior.EngineRunning)
        {action.Watcher=null;action.RetireNeverAdmittedTree();return false;}
        return behavior.StartActing(action,completion is null?null:r=>{if(CurrentAt(version)&&CanRun)completion(r);},
            ()=>CurrentAt(version)&&CanRun&&behavior.EngineRunning);
    }
}

// fidelity: M7-021
// WaitForLambdaAction ctor55B554 (type33, tracks0, predicate+78, timeout+90), Init55DADA,
// Check55DADE, timeout55DAF6. IAction owns the timeout/result and ActionList completion.
internal sealed class NativePredicateWaitRunner:NativeVisionRunner
{
    private readonly Func<bool> _predicate;
    internal NativePredicateWaitRunner(ManipulationSystem m,EngineRobot owner,Func<bool> predicate,float timeout)
        :base(m,0x33,0){ScopedOwner=owner;_predicate=predicate;TimeoutSeconds=timeout;}
    protected override string ActionName=>"WaitForLambda";
    public override byte? GetCompletionVariant()=>6;
    public override int Init()=>ScopedCurrent?0:unchecked((int)EngineActionResult.Cancelled);
    public override uint CheckIfDone()
    {if(!ScopedCurrent)return EngineActionResult.Cancelled;bool done=_predicate();return !ScopedCurrent?EngineActionResult.Cancelled:done?0:EngineActionResult.Running;}
}

public sealed partial class ReactToImpactBehavior
{
    private readonly PhysicalReactionOwner? _physical;
    private Action? _detachPhysicalSignals;
    internal void RetireNativePhysical(){_physical?.Retire();_detachPhysicalSignals?.Invoke();_detachPhysicalSignals=null;RetireActionCompletionOwner();}
    internal ReactToImpactBehavior(ManipulationSystem m,EngineRobot owner,string id):this(m.Robot,id)
    {_physical=new(m,owner);BindActionCompletionOwner(m.Robot,owner);}
    protected override bool KeepsRunningWithoutAction=>_physical is not null && HasCurrentAction;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>_physical?.Advance();
    protected override long CaptureHostExecutionVersion()=>_physical?.Version??0;
    protected override bool HostExecutionVersionCurrent(long version)=>_physical?.CurrentAt(version)??true;
    protected override bool HostStopContinuationCurrent(long version)=>_physical is null||_physical.Version==unchecked(version+1);
    public override bool IsRunnable(BehaviorContext context)
    {using var execution=EnterHostExecutionOwner(context);return _physical is null?base.IsRunnable(context):_physical.CanRun&&IsRunnableBase(context)&&_physical.CanRun;}
    private void NativeImpactWait()
    {
        var native=_physical!;long version=native.Version;
        // 60621A..606254: WaitForLambda(robot, stopped && calibrated,5.0f), member callback.
        native.Start(this,new NativePredicateWaitRunner(native.M,native.Robot,()=>_fallingStoppedSeen&&_calibrated,5f),
            result=>{WaitedOut=result.Result!=0;TransitionToPlayingAnim();},version);
    }
    private void NativeImpactAnimation()
    {
        var native=_physical!;long version=native.Version;
        // 60635A..606392: only impact>1000; TriggerAnimationAction(robot,1A0,1,true,0,60,false), empty callback.
        var action=native.Prepare(new TriggerAnimationAction(native.M.Robot,new(AnimationTrigger.ReactToImpact,1,true,0,60f,false)));
        native.Start(this,action,null,version);
    }
}

public sealed partial class ReactToPlacedOnSlopeBehavior
{
    private readonly PhysicalReactionOwner? _physical;
    internal void RetireNativePhysical(){_physical?.Retire();RetireActionCompletionOwner();}
    private double _nativeLastRunSeconds; // ctor607FC2: +120 = double0
    private static readonly float NativeDegreesPerRadian=BitConverter.UInt32BitsToSingle(0x42652EE1);
    internal ReactToPlacedOnSlopeBehavior(ManipulationSystem m,EngineRobot owner,string id):base(id,"ReactToPlacedOnSlope")
    {_physical=new(m,owner);BindActionCompletionOwner(m.Robot,owner);}
    protected override bool RunnableGate20(BehaviorContext context)=>true; // vtable102C344+8+20 ->608280
    protected override bool RunnableGate24(BehaviorContext context)=>false; // 59EC12
    protected override bool RunnableGate28(BehaviorContext context)=>true; // 608284
    protected override bool KeepsRunningWithoutAction=>_physical is not null && HasCurrentAction;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>_physical?.Advance();
    protected override long CaptureHostExecutionVersion()=>_physical?.Version??0;
    protected override bool HostExecutionVersionCurrent(long version)=>_physical?.CurrentAt(version)??true;
    protected override bool HostStopContinuationCurrent(long version)=>_physical is null||_physical.Version==unchecked(version+1);
    public override bool IsRunnable(BehaviorContext context)
    {using var execution=EnterHostExecutionOwner(context);return _physical is null?base.IsRunnable(context):_physical.CanRun&&IsRunnableBase(context)&&_physical.CanRun;}
    protected override void OnStop(BehaviorStopReason reason)=>_physical?.Advance(); // native StopInternal is empty
    private void NativeSlopeStart()
    {
        var native=_physical!;long version=native.Version;
        // 608002..608026: timer float seconds promoted to double, strict elapsed<10.
        double now=native.Robot.Engine.Timer.SecondsF;
        if(now-_nativeLastRunSeconds<10d && PitchWasHigh)
        {
            float degrees=(native.M.Robot.Sensors.PitchRad??0)*NativeDegreesPerRadian;
            native.Robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"BehaviorReactToPlacedOnSlope.CalibratingHead",
                degrees.ToString("F6",CultureInfo.InvariantCulture)); // 608036..608056
            if(!native.CurrentAt(version))return;
            // 60807C..60809A: CalibrateMotorAction(robot,true,false), empty callback.
            native.Start(this,new NativeHeadCalibrationRunner(native.M,native.Robot),null,version);
            if(!native.CurrentAt(version))return;
            PitchWasHigh=false; // 608104..608106 clears immediately, not on calibration completion
        }
        else
        {
            int need=(Context.Needs??native.Robot.Engine.Needs).SevereNeeds.Selected;
            // 6080B2..6080F8: TriggerAnimationAction(robot, need0?145:need1?139:1A8,1,true,0,60,false).
            var action=native.Prepare(new TriggerAnimationAction(native.M.Robot,new((AnimationTrigger)(need==0?0x145:need==1?0x139:0x1A8),1,true,0,60f,false)));
            native.Start(this,action,_=>
            {
                // 608240..60826A: signed pitch degrees>10, stores byte only; no extra action/finish.
                float degrees=(native.M.Robot.Sensors.PitchRad??0)*NativeDegreesPerRadian;
                PitchWasHigh=degrees>10f;
            },version);
        }
        if(native.CurrentAt(version))_nativeLastRunSeconds=now; //60810C
    }
}

public sealed partial class ReactToCliffBehavior
{
    private readonly PhysicalReactionOwner? _physical;
    private Action? _detachPhysicalSignals;
    internal void RetireNativePhysical(){_physical?.Retire();_detachPhysicalSignals?.Invoke();_detachPhysicalSignals=null;RetireActionCompletionOwner();}
    internal ReactToCliffBehavior(ManipulationSystem m,EngineRobot owner,string id):this(m.Robot,id)
    {_physical=new(m,owner);BindActionCompletionOwner(m.Robot,owner);}
    public override bool IsRunnable(BehaviorContext context)
    {using var execution=EnterHostExecutionOwner(context);return _physical is null?base.IsRunnable(context):_physical.CanRun&&IsRunnableBase(context)&&_physical.CanRun;}
    private bool NativeCliffStart(ActionRunner action,Action<RobotCompletedAction>? completion,long version)
    {
        if(!HostExecutionVersionCurrent(version)||!EngineRunning)
        {action.Watcher=null;action.RetireNeverAdmittedTree();return false;}
        return StartActing(action,completion is null?null:r=>{if(HostExecutionVersionCurrent(version))completion(r);},
            ()=>HostExecutionVersionCurrent(version)&&EngineRunning);
    }
    private TriggerAnimationAction NativeCliffAnimation(AnimationTrigger trigger)=>_physical!.Prepare(
        new TriggerLiftSafeAnimationAction(_physical.M.Robot,new(trigger,1,true,0,60f,false)));
    private void NativeCliffWait(int severe)
    {
        var native=_physical!;long version=CaptureHostExecutionVersion();
        // 604DCA..604E82: saved threshold; WaitForLambda(robot,$_0,FLT_MAX), selected member callback.
        NativeCliffStart(new NativePredicateWaitRunner(native.M,native.Robot,InitWaitLambda,float.MaxValue)
            {ScopedParentCurrent=()=>HostExecutionVersionCurrent(version)&&EngineRunning},
            _=>{if(severe>=2)TransitionToPlayingStopReaction();else TransitionToPlayingCliffReaction();},version);
    }
    private void NativeCliffStopAnimation()
    {
        var native=_physical!;long version=CaptureHostExecutionVersion();
        var parallel=new CompoundActionParallel(()=>native.Robot.Engine.Timer.SecondsF)
            {SourceLog=(l,c,k,t)=>native.Robot.Engine.EmitSourceLog(l,c,k,t)};
        parallel.SeedNativeEmptyCompletion();
        // 604FB0..605044: Add(animation,false,false), Add(wait,true,false), same parent action.
        parallel.AddAction(NativeCliffAnimation(AnimationTrigger.ReactToCliffDetectorStop),false,false);
        parallel.AddAction(new NativePredicateWaitRunner(native.M,native.Robot,()=>_reactNow!=0,StopWaitTimeout),true,false);
        NativeCliffStart(parallel,_=>TransitionToPlayingCliffReaction(),version); //605062..60506E
    }
    private void NativeCliffReactionAnimation(AnimationTrigger trigger)
    {
        long version=CaptureHostExecutionVersion();
        NativeCliffStart(NativeCliffAnimation(trigger),_=>TransitionToBackingUp(),version); //605170..6051BC
    }
    private void NativeCliffBackUp()
    {
        var native=_physical!;long version=CaptureHostExecutionVersion();
        // 605332..60536C: DriveStraightAction(robot,-60,100,true), resultless completion.
        NativeCliffStart(new DriveStraightAction(native.M,native.Robot,-60f,100f,true),_=>SendFinishedReactToCliffMessage(),version);
    }
}

public sealed partial class ReactToUnexpectedMovementBehavior
{
    private readonly PhysicalReactionOwner? _physical;
    private Action? _detachPhysicalSignals;
    internal void RetireNativePhysical(){_physical?.Retire();_detachPhysicalSignals?.Invoke();_detachPhysicalSignals=null;RetireActionCompletionOwner();}
    internal ReactToUnexpectedMovementBehavior(ManipulationSystem m,EngineRobot owner,string id):this(m.Robot,id)
    {_physical=new(m,owner);}
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>context?.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>_physical?.Advance();
    protected override long CaptureHostExecutionVersion()=>_physical?.Version??0;
    protected override bool HostExecutionVersionCurrent(long version)=>_physical?.CurrentAt(version)??true;
    protected override bool HostStopContinuationCurrent(long version)=>_physical is null||_physical.Version==unchecked(version+1);
    protected override void OnStop(BehaviorStopReason reason)=>_physical?.Advance(); //609C1E empty derived Stop
    public override bool IsRunnable(BehaviorContext context)
    {using var execution=EnterHostExecutionOwner(context);return _physical is null?base.IsRunnable(context):_physical.CanRun&&IsRunnableBase(context)&&_physical.CanRun;}
}
