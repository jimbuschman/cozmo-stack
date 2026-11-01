using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured Hiking_PounceOnMotion: bounded no-camera/no-delivered-motion path.
internal sealed class NativeHikingPounce : SteppedBehavior, IPounceScoreState, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly BehaviorManager? _manager;
    private readonly bool _valid;
    private readonly long _initialFrames;
    private readonly RetainedSubscription? _unsupportedMotion;
    private readonly Action<OffTreadsState,OffTreadsState>? _treads;
    private readonly Action<CliffReport>? _unsupportedCliff;
    private int _unsupportedEvent,_retired;
    private long _generation;
    private bool _blocked,_liftDisabled;
    private ScopedAcquisition? _drivingLease,_idleLease;
    private float _lastMotion=-1000f,_lastCliff,_started,_lastRotate;
    internal int NativeState {get;private set;}
    internal bool LiftDisabled=>_liftDisabled;
    public float LastGroundMotionSeconds=>_lastMotion;
    public float NotRunningBoredSeconds=>1.5f;
    public float BoredomMultiplier=>BitConverter.UInt32BitsToSingle(0x3F4CCCCD);
    internal NativeHikingPounce(BehaviorFactoryContext source,JsonElement config):base("Hiking_PounceOnMotion","PounceOnMotion")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;_manager=_robot.BehaviorManagerOwner;_initialFrames=_m.Vision.MotionFrameInputs;
        try
        {
            _valid=config.GetProperty("maxNoGroundMotionBeforeBored_running_Sec").GetSingle()==3f &&
                config.GetProperty("maxNoGroundMotionBeforeBored_notRunning_Sec").GetSingle()==1.5f &&
                config.GetProperty("searchAmplitudeDeg").GetSingle()==90f &&
                (!config.TryGetProperty("alwaysStreamline",out var always) || always.ValueKind==JsonValueKind.False || always.ValueKind==JsonValueKind.Null) &&
                !config.TryGetProperty("boredomMultiplier",out _) && !config.TryGetProperty("maxTimeBehaviorTimeout_Sec",out _) &&
                !config.TryGetProperty("timeBeforeRotate_Sec",out _);
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {ReportMissing("Hiking_PounceOnMotion parameters unavailable: "+e.Message);}
        if(_robot.HasExternalInterface)
        {
            // This is an unsupported-delivery guard, not a claim that the M11-035 producer exists.
            _unsupportedMotion=_robot.Engine.AnimationMessages.SubscribeMessage(0x49,true,_=>Interlocked.Exchange(ref _unsupportedEvent,1));
            _treads=(_,_)=>{using var owner=_robot.Engine.EnterSynchronousOwner();if(Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot))_lastCliff=0;};
            _m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged+=_treads;
            // This host callback only withdraws the no-cliff proof; native tag-22 phase remains unresolved.
            _unsupportedCliff=_=>Interlocked.Exchange(ref _unsupportedEvent,1);
            _m.Robot.Sensors.CliffDetectedInternal+=_unsupportedCliff;
        }
    }
    private bool OwnerCurrent=>_valid && NativeScoringAvailable && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        ReferenceEquals(_robot.BehaviorManagerOwner,_manager) && _manager is not null && _robot.NativeBehaviorRuntime is {} runtime &&
        ReferenceEquals(runtime.Manipulation,_m) && runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this);
    private bool Supported()
    {
        if(!OwnerCurrent || _manager!.ActiveSparkId!=0x55 || _initialFrames!=0 || _m.Vision.MotionFrameInputs!=_initialFrames || Volatile.Read(ref _unsupportedEvent)!=0)
        {MarkReadinessUnavailable();ReportMissing("M11-035/M15 Pounce no-motion execution needs original owner, ordinary Spark state and known no-frame/no-unsupported-event history");return false;}
        return true;
    }
    internal override bool HostScoringInputsAvailable()
    {
        if(_blocked){MarkReadinessUnavailable();ReportMissing("Pounce source execution is held at an unavailable input");return false;}
        return Supported();
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long value)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==value && ReferenceEquals(_robot.Engine.Robot,_robot) && ReferenceEquals(_robot.BehaviorManagerOwner,_manager);
    private bool Current(long value)
    {
        if(!HostExecutionVersionCurrent(value))return false;
        if(_blocked)return false;
        if(Supported())return true;
        _blocked=true;return false;
    }
    protected override bool HostStopContinuationCurrent(long value)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(value+1);
    protected override bool HostResumeContinuationCurrent(long value)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==value || Interlocked.Read(ref _generation)==unchecked(value+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction||_blocked;
    protected override bool HoldUnavailableActionCompletion {get {if(!Supported())_blocked=true;return _blocked;}}
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(_blocked){MarkReadinessUnavailable();ReportMissing("Pounce source execution is held at an unavailable input");return false;}
        if(!Supported())return false;
        return base.IsRunnable(context);
    }
    protected override void OnStart()
    {
        if(!Supported()){InitFailed=true;return;}
        _blocked=false;long g=CaptureHostExecutionVersion();if(!InitHelper(g))return;
        NativeState=1;float now=_robot.Engine.Timer.SecondsF;
        NativePanTiltRunner? optional=null;
        if((float)(now-_lastCliff)<10f)optional=Pan(BitConverter.UInt32BitsToSingle(0x3FC90FDB),0,false,false);
        if(!Current(g)){optional?.RetireNeverAdmittedTree();return;}
        var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);
        if(optional is not null)sequence.AddAction(optional,false,false);
        sequence.AddAction(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0x196,1,true,0,60f,false)),false,false);
        if(!Current(g)){sequence.RetireNeverAdmittedTree();return;}
        Run(sequence,()=>DisableLiftThenWait(g,()=>InitialReaction(g)),g);
    }
    private bool InitHelper(long g)
    {
        _lastMotion=_started=_robot.Engine.Timer.SecondsF;
        var scheduler=_m.Robot.Animations.Scheduler;
        var idle=new ScopedAcquisition();_idleLease=idle;
        if(!Scope.SmartPushIdleAnimationGuarded(()=>scheduler.PushScopedIdle((AnimationTrigger)0x192,Id,idle),()=>{idle.Cancel();scheduler.RemoveScopedIdle(Id,idle,_robot.Engine.Timer.TimeStampMs);},()=>Current(g)))return false;
        if(!Current(g))return false;
        idle.Completed=true;
        var driving=new ScopedAcquisition();_drivingLease=driving;
        _m.Robot.Animations.DrivingAnimations.PushOwned(new((AnimationTrigger)0x191,(AnimationTrigger)0x190,(AnimationTrigger)0x18F),Id,driving);
        if(!Current(g))return false;driving.Completed=true;return true;
    }
    private NativePanTiltRunner Pan(float body,float head,bool bodyAbsolute,bool headAbsolute)
        =>new(_m,new PanAndTiltAction{PanAngleRad=body,HeadAngleRad=head,PanIsAbsolute=bodyAbsolute,HeadIsAbsolute=headAbsolute});
    private void Run(ActionRunner action,Action? next,long g)
    {
        if(!Current(g)){action.RetireNeverAdmittedTree();return;}
        StartActing(action,next is null?null:_=>{if(Current(g))next();},()=>Current(g));
    }
    private void DisableLiftThenWait(long g,Action next)
    {
        if(!Current(g))return;
        // The transport can synchronously call out after enqueueing. Treat the disable as
        // pending ownership so a reentrant Stop restores power before this call returns.
        _liftDisabled=true;
        if(!_m.Robot.Motion.SetNativeLiftPower(_robot,false)){if(Current(g)){_liftDisabled=false;_blocked=true;ReportMissing("Pounce EnableLiftPower(false) original owner/send unavailable");}return;}
        if(!Current(g))return;NativeState=10;
        Run(new NativeTimedWaitRunner(_m,BitConverter.UInt32BitsToSingle(0x3E19999A)),()=>
        {
            if(!Current(g))return;
            _liftDisabled=false;
            if(!_m.Robot.Motion.SetNativeLiftPower(_robot,true)){if(Current(g)){_liftDisabled=true;_blocked=true;ReportMissing("Pounce EnableLiftPower(true) original owner/send unavailable");}return;}
            if(!Current(g))return;next();
        },g);
    }
    private void InitialReaction(long g)
    {
        if(!Current(g))return;NativeState=2;
        if(_robot.StoredState is not {} state){_blocked=true;ReportMissing("Pounce actual pitch/lift state unavailable");return;}
        bool caught=state.Pose.Pitch>BitConverter.UInt32BitsToSingle(0x3CA3D70A) || state.LiftHeightMm>35.5f;
        if(caught)Run(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0x197,1,true,0,60f,false)),()=>InitialSearch(g),g);
        else InitialSearch(g);
    }
    private void InitialSearch(long g)
    {
        if(!Current(g))return;NativeState=3;var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);
        const double deg=0.017453292519943295;
        float first=(float)(_robot.Engine.ContextRandom.RandDblInRange(20,45)*deg);
        int sign=_robot.Engine.ContextRandom.RandDbl(1)<.5?-1:1;first*=sign;
        sequence.AddAction(Pan(first,0,false,true),false,false);
        float second=(float)(_robot.Engine.ContextRandom.RandDblInRange(20,45)*deg);second*=-sign;
        sequence.AddAction(Pan(second,0,false,true),false,false);
        if(!Current(g)){sequence.RetireNeverAdmittedTree();return;}
        Run(sequence,()=>HeadDown(g),g);
    }
    private void HeadDown(long g)
    {
        if(!Current(g))return;Scope.SmartUnLockTracks("behaviorPounceOnMotionWaitLock");if(!Current(g))return;NativeState=4;
        Run(_m.Robot.Motion.CreateNativeHeadChild(CozmoMotion.MinHeadAngleRad,BitConverter.UInt32BitsToSingle(0x3D0EFA35),CozmoMotion.ActionDefaultHeadSpeedRadPerSec,CozmoMotion.ActionDefaultHeadAccelRadPerSec2),()=>WaitForMotion(g),g);
    }
    private void WaitForMotion(long g)
    {
        if(!Current(g))return;NativeState=6;
        if(!Scope.SmartLockTracks("behaviorPounceOnMotionWaitLock",AnimationTrack.Head))return;
        if(!Current(g))return;Run(new NativeTimedWaitRunner(_m,2f),()=>AfterWait(g),g);
    }
    private void AfterWait(long g)
    {
        if(!Current(g))return;Scope.SmartUnLockTracks("behaviorPounceOnMotionWaitLock");if(!Current(g))return;
        float now=_robot.Engine.Timer.SecondsF;
        if(_lastMotion+3f<now){GetOut(g);return;}
        if(_lastRotate+5f<now){_blocked=true;ReportMissing("MISSING: Pounce RotateWatchingNewArea 0x005F8FD8 is not in the released normal path");return;}
        if(_started+30f<now){GetOut(g);return;}
        WaitForMotion(g);
    }
    private void GetOut(long g)
    {
        if(!Current(g))return;NativeState=13;
        Run(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0x194,1,true,0,60f,false)),null,g);
    }
    protected override void OnUpdate(){if(!Supported())_blocked=true;}
    protected override int ResumeInternal()
    {
        if(!Supported())return 1;_blocked=false;long g=CaptureHostExecutionVersion();
        if(!InitHelper(g))return 1;HeadDown(g);return 0;
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        long g=CaptureHostExecutionVersion();NativeState=14;
        if(_liftDisabled){_liftDisabled=false;if(!_m.Robot.Motion.SetNativeLiftPower(_robot,true))ReportMissing("Pounce Stop lift power restoration unavailable");}
        if(!HostExecutionVersionCurrent(g))return;
        Scope.SmartRemoveIdleAnimation();_idleLease=null;
        if(!HostExecutionVersionCurrent(g))return;
        if(_drivingLease is {} driving)
        {
            _drivingLease=null;driving.Cancel();
            if(driving.Completed)_m.Robot.Animations.DrivingAnimations.RemoveScoped(Id,()=>HostExecutionVersionCurrent(g));
            else _m.Robot.Animations.DrivingAnimations.RemoveOwned(driving);
        }
        if(!HostExecutionVersionCurrent(g))return;
        _blocked=false;Interlocked.Increment(ref _generation);
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);_unsupportedMotion?.Dispose();
        if(_treads is not null)_m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged-=_treads;
        if(_unsupportedCliff is not null)_m.Robot.Sensors.CliffDetectedInternal-=_unsupportedCliff;
        RetireActionCompletionOwner();
    }
}
