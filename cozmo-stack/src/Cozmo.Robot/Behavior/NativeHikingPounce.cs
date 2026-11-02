using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-107
// Configured Hiking_PounceOnMotion. Native body 0x005F7F90..0x005FA194, read against this file 2026-11-02.
internal sealed class NativeHikingPounce : SteppedBehavior, IPounceScoreState, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly BehaviorManager? _manager;
    private readonly bool _valid;
    private readonly RetainedSubscription? _motion;
    private readonly Action<OffTreadsState,OffTreadsState>? _treads;
    private readonly Action<CliffReport>? _cliff;
    private int _retired;
    private long _generation;
    private bool _blocked,_liftDisabled;
    private ScopedAcquisition? _drivingLease,_idleLease;
    private float _lastMotion=-1000f,_lastCliff,_started,_lastRotate;
    private float _lastValid,_distance,_driveDistance,_pitchBaseline,_accumulatedYaw;
    private short _imageX,_imageY;
    private int _validCount,_creepCount;
    private bool _gotValid,_pendingPoint;
    internal int NativeState {get;private set;}
    internal bool LiftDisabled=>_liftDisabled;
    public float LastGroundMotionSeconds=>_lastMotion;
    public float NotRunningBoredSeconds=>1.5f;
    public float BoredomMultiplier=>BitConverter.UInt32BitsToSingle(0x3F4CCCCD);
    internal NativeHikingPounce(BehaviorFactoryContext source,JsonElement config):base("Hiking_PounceOnMotion","PounceOnMotion")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;_manager=_robot.BehaviorManagerOwner;
        try
        {
            _valid=config.GetProperty("maxNoGroundMotionBeforeBored_running_Sec").GetSingle()==3f &&
                config.GetProperty("maxNoGroundMotionBeforeBored_notRunning_Sec").GetSingle()==1.5f &&
                config.GetProperty("searchAmplitudeDeg").GetSingle()==90f &&
                (!config.TryGetProperty("alwaysStreamline",out var always) || always.ValueKind==JsonValueKind.False || always.ValueKind==JsonValueKind.Null) &&
                !config.TryGetProperty("boredomMultiplier",out _) && !config.TryGetProperty("maxTimeBehaviorTimeout_Sec",out _) &&
                !config.TryGetProperty("timeBeforeRotate_Sec",out _) &&
                !config.TryGetProperty("skipGetOutAnim",out _) && !config.TryGetProperty("oddsOfPouncingOnTurn",out _); // 0x005F81F6, 0x005F817A: not in shipped Hiking
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {ReportMissing("Hiking_PounceOnMotion parameters unavailable: "+e.Message);}
        if(_robot.HasExternalInterface)
        {
            _motion=_robot.Engine.AnimationMessages.SubscribeMessage(0x49,true,p=>{if(p is NativeObservedMotion motion)HandleMotion(motion);});
            _treads=(_,_)=>{using var owner=_robot.Engine.EnterSynchronousOwner();if(Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot))_lastCliff=0;};
            _m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged+=_treads;
            _cliff=report=>{using var owner=_robot.Engine.EnterSynchronousOwner();if(Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) && (byte)report.Sensors!=0)_lastCliff=_robot.Engine.Timer.SecondsF;};
            _m.Robot.Sensors.ConfiguredBehaviorCliffDetected+=_cliff;
        }
    }
    private bool OwnerCurrent=>_valid && NativeScoringAvailable && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        ReferenceEquals(_robot.BehaviorManagerOwner,_manager) && _manager is not null && _robot.NativeBehaviorRuntime is {} runtime &&
        ReferenceEquals(runtime.Manipulation,_m) && runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this);
    private bool Supported()
    {
        if(!OwnerCurrent || _manager!.ActiveSparkId!=0x55 || !_m.Vision.NativeMotionReadyFor(_robot))
        {MarkReadinessUnavailable();ReportMissing("M11-098/M15-107 Pounce needs its original owner's live motion producer");return false;}
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
        _blocked=false;_gotValid=false;long g=CaptureHostExecutionVersion();if(!InitHelper(g))return;
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
        _lastMotion=_started=_robot.Engine.Timer.SecondsF;_creepCount=0;
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
        bool caught=state.Pose.Pitch-_pitchBaseline>BitConverter.UInt32BitsToSingle(0x3CA3D70A) || state.LiftHeightMm>35.5f;
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
        Run(sequence,()=>WaitForMotion(g),g); // 0x005F89BE..0x005F89CA
    }
    private void HeadDown(long g)
    {
        if(!Current(g))return;Scope.SmartUnLockTracks("behaviorPounceOnMotionWaitLock");if(!Current(g))return;NativeState=4;
        Run(_m.Robot.Motion.CreateNativeHeadChild(CozmoMotion.MinHeadAngleRad,BitConverter.UInt32BitsToSingle(0x3D0EFA35),CozmoMotion.ActionDefaultHeadSpeedRadPerSec,CozmoMotion.ActionDefaultHeadAccelRadPerSec2),()=>WaitForMotion(g),g);
    }
    private void WaitForMotion(long g)
    {
        if(!Current(g))return;NativeState=6;_driveDistance=0;_validCount=0;_pendingPoint=false;
        if(!Scope.SmartLockTracks("behaviorPounceOnMotionWaitLock",AnimationTrack.Head))return;
        if(!Current(g))return;Run(new NativeTimedWaitRunner(_m,2f),()=>AfterWait(g),g);
    }
    private void AfterWait(long g)
    {
        if(!Current(g))return;Scope.SmartUnLockTracks("behaviorPounceOnMotionWaitLock");if(!Current(g))return;
        if(_pendingPoint){TurnToMotion(g);return;}
        float now=_robot.Engine.Timer.SecondsF;
        if(_lastMotion+3f<now){GetOut(g);return;}
        if(_lastRotate+5f<now){RotateWatchingNewArea(g);return;}
        if(_started+30f<now){GetOut(g);return;}
        WaitForMotion(g);
    }
    private void HandleMotion(NativeObservedMotion report)
    {
        using var owner=_robot.Engine.EnterSynchronousOwner();
        if(!OwnerCurrent)return;
        bool ground=report.GroundArea>BitConverter.UInt32BitsToSingle(0x3C23D70A);
        float x=report.GroundX,y=report.GroundY;float distanceSquared=x*x+y*y;
        if(!EngineRunning)
        {
            if(ground && distanceSquared<=120f*120f)_lastMotion=_robot.Engine.Timer.SecondsF;
            return;
        }
        float now=_robot.Engine.Timer.SecondsF;
        if(ground)_lastMotion=now;
        if(NativeState!=6)return;
        float distance=(float)Math.Sqrt((double)x*x+(double)y*y);
        if(ground && distance<=120f) // +0x11C=120f 0x005F7FA0..0x005F7FAC
        {
            _lastValid=now;_gotValid=true;_validCount=unchecked(_validCount+1);_distance=distance;
            _imageX=report.ImageX;_imageY=report.ImageY;_pendingPoint=true;
            StopActing(keepAction:true,viaCallback:false);
        }
        else if(_validCount>=1 && now>=_lastValid+4f)_validCount=0;
    }
    private void TurnToMotion(long g)
    {
        if(!Current(g))return;NativeState=7;_lastRotate=_robot.Engine.Timer.SecondsF;
        if(_m.Vision.Calibration is not {} camera){_blocked=true;ReportMissing("M11-098/M15-107 Pounce camera calibration unavailable at TurnToMotion");return;}
        float dx=(float)_imageX-(float)camera.CenterX,dy=(float)_imageY-(float)camera.CenterY;
        _=MathF.Atan(-dy/(float)camera.FocalLengthY); // source computes tilt first; action uses zero head.
        float pan=MathF.Atan(-dx/(float)camera.FocalLengthX);
        bool odds=false;
        if(_creepCount>=3)odds=(double)_creepCount*.2>_robot.Engine.ContextRandom.RandDbl(1);
        bool pounce=_distance<=65f || _distance-50f<10f || odds;
        Action next=pounce?()=>Pounce(g):()=>CreepForward(g);
        if(!pounce)_creepCount=unchecked(_creepCount+1);
        Run(Pan(pan,0,false,false),next,g);
    }
    private void CreepForward(long g)
    {
        if(!Current(g))return;NativeState=8;_driveDistance=_distance-50f;
        var action=new DriveStraightAction(_m,_robot,_driveDistance,60f,true);
        if(!Scope.SmartLockTracks("behaviorPounceOnMotionWaitLock",AnimationTrack.Head)){action.RetireNeverAdmittedTree();return;}
        Run(action,()=>HeadDown(g),g);
    }
    private void Pounce(long g)
    {
        if(!Current(g))return;NativeState=9;
        if(_robot.StoredState is not {} state){_blocked=true;ReportMissing("M15-107 original pitch input unavailable");return;}
        _pitchBaseline=state.Pose.Pitch;if(_driveDistance<=0)_driveDistance=_distance-50f;
        PounceSequence(g,null,()=>ResultAnimation(g));
    }
    private void PounceSequence(long g,ActionRunner? optional,Action next)
    {
        var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);
        if(optional is not null)sequence.AddAction(optional,false,false);
        sequence.AddAction(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0x196,1,true,0,60f,false)),false,false);
        if(!Current(g)){sequence.RetireNeverAdmittedTree();return;}
        Run(sequence,()=>DisableLiftThenWait(g,next),g);
        _creepCount=0; // 0x005F8B36..0x005F8B38, after StartActing
    }
    private void ResultAnimation(long g)
    {
        if(!Current(g))return;NativeState=11;
        if(_robot.StoredState is not {} state){_blocked=true;ReportMissing("M15-107 original pitch/lift input unavailable");return;}
        float delta=state.Pose.Pitch-_pitchBaseline;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Behaviors","BehaviorPounceOnMotion.CheckResult",
            FormattableString.Invariant($"lift: {state.LiftHeightMm:F6} body: {delta*57.29578f:F6}deg ({delta:F6}rad) ({_pitchBaseline*57.29578f:F6} -> {state.Pose.Pitch*57.29578f:F6})"));
        if(!Current(g))return;
        if(_robot.StoredState is not {} current){_blocked=true;ReportMissing("M15-107 original lift input unavailable after CheckResult");return;}
        bool caught=delta>BitConverter.UInt32BitsToSingle(0x3CA3D70A) || current.LiftHeightMm>35.5f;
        _validCount=0;
        Run(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)(caught?0x197:0x193),1,true,0,60f,false)),_driveDistance>0?()=>Backup(g):()=>HeadDown(g),g);
        if(caught && Current(g))_robot.Engine.ReportBehaviorObjective(23,"PouncedAndCaught",Id);
    }
    private void Backup(long g)
    {
        if(!Current(g))return;NativeState=12;
        float distance=BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(_driveDistance)^0x80000000u);
        Run(new DriveStraightAction(_m,_robot,distance,80f,true),()=>HeadDown(g),g);
    }
    private void RotateWatchingNewArea(long g)
    {
        if(!Current(g))return;NativeState=5;_lastRotate=_robot.Engine.Timer.SecondsF;
        float amount=(float)(_robot.Engine.ContextRandom.RandDblInRange(20,45)*0.017453292519943295);
        double draw=_robot.Engine.ContextRandom.RandDbl(1);
        double amplitude=BitConverter.UInt32BitsToSingle(0x3FC90FDB);
        double probability=(amplitude-(double)_accumulatedYaw);
        probability=(amplitude/probability)*.5d;
        if(draw<probability)amount=-amount;
        _accumulatedYaw=new Radians(_accumulatedYaw+amount).Value;
        var action=Pan(amount,0,false,false);
        float chance=(float)_robot.Engine.ContextRandom.RandDbl(1); // shipped pounceChance is zero
        if(0f>chance)PounceSequence(g,action,()=>WaitForMotion(g));else Run(action,()=>WaitForMotion(g),g);
    }
    private void GetOut(long g)
    {
        if(!Current(g))return;NativeState=13;
        Run(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0x194,1,true,0,60f,false)),null,g);
    }
    protected override void OnUpdate(){if(!Supported())_blocked=true;}
    protected override int ResumeInternal()
    {
        if(!Supported())return 1;_blocked=false;_pendingPoint=false;long g=CaptureHostExecutionVersion(); // +0x164 0x005F8580
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
        _lastValid=0;_validCount=0;_imageX=0;_imageY=0;_pendingPoint=false;
        if(_gotValid){_gotValid=false;NeedActionCompleted();if(!HostExecutionVersionCurrent(g))return;}
        _blocked=false;Interlocked.Increment(ref _generation);
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);_motion?.Dispose();
        if(_treads is not null)_m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged-=_treads;
        if(_cliff is not null)_m.Robot.Sensors.ConfiguredBehaviorCliffDetected-=_cliff;
        RetireActionCompletionOwner();
    }
}
