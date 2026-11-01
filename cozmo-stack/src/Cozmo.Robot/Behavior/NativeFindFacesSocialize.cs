using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured FindFaces_socialize and its inherited scan-in-place action core.
internal sealed class NativeFindFacesSocialize : SteppedBehavior, ISocializeFindFaces, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly float[] _bodyMin=new float[7],_bodyMax=new float[7],_headMin=new float[7],_headMax=new float[7];
    private readonly float _bodySpeed,_tiltSpeed,_headSpeed,_chance,_focus;
    private readonly byte _changesMin,_changesMax;
    private readonly uint _maxAge;
    private readonly AnimationTrigger _pauseBody,_pauseHead;
    private readonly bool _valid;
    private readonly Action<OffTreadsState,OffTreadsState>? _treads;
    private float _center,_baseline,_headBaseline,_accumulated;
    private byte _direction,_endpoints,_remaining,_total;
    private uint _iterations;
    private long _generation;
    private int _retired;
    public uint SearchIterations=>_iterations;
    internal int NativeState {get;private set;}
    internal bool ParametersAvailable=>_valid;
    private bool Available=>_valid && NativeScoringAvailable && BehaviorObjective==BehaviorObjective.Invalid && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} r && ReferenceEquals(r.Manipulation,_m) && r.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this) && ReferenceEquals(_m.World,_m.Robot.LocalizationWorld) &&
        _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null && _robot.MainPose.Node is not null;
    internal NativeFindFacesSocialize(BehaviorFactoryContext source,JsonElement root):base("FindFaces_socialize","FindFaces")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        try
        {
            var p=root.GetProperty("params");float F(string key)=>p.GetProperty(key).GetSingle();
            _bodySpeed=F("sx_BodyTurnSpeed_degPerSec");_tiltSpeed=F("sxt_HeadTurnSpeed_degPerSec");_headSpeed=F("sxh_HeadTurnSpeed_degPerSec");
            _chance=F("s0_MainTurnCWChance");_focus=F("behavior_AngleOfFocus_deg");
            foreach(int n in new[]{1,3,4,5,6})
            {
                string b=n is 4 or 5?$"s{n}_BodyAngleRelativeRange":$"s{n}_BodyAngleRange";
                _bodyMin[n]=F(b+"Min_deg");_bodyMax[n]=F(b+"Max_deg");
                _headMin[n]=F($"s{n}_HeadAngleRangeMin_deg");_headMax[n]=F($"s{n}_HeadAngleRangeMax_deg");
            }
            _changesMin=unchecked((byte)p.GetProperty("s4_HeadAngleChangesMin").GetInt32());_changesMax=unchecked((byte)p.GetProperty("s4_HeadAngleChangesMax").GetInt32());
            _pauseBody=Enum.Parse<AnimationTrigger>(p.GetProperty("s2_WaitAnimTrigger").GetString()!);
            _pauseHead=Enum.Parse<AnimationTrigger>(p.GetProperty("s4_WaitAnimTrigger").GetString()!);
            _maxAge=root.TryGetProperty("maxFaceAgeToLook_ms",out var age)?age.GetUInt32():0;
            _valid=p.GetProperty("behavior_ShouldResetTurnDirection").GetBoolean() && p.GetProperty("behavior_ResetBodyFacingOnStart").GetBoolean() &&
                p.GetProperty("behavior_ShouldLowerLift").GetBoolean() && !p.GetProperty("behavior_CanCarryCube").GetBoolean() &&
                p.GetProperty("behavior_RecentLocationsMax").GetInt32()==0 && _focus==30f && p.GetProperty("behavior_NumberOfScansBeforeStop").GetInt32()==0 &&
                _pauseBody==AnimationTrigger.LookInPlaceForFacesBodyPause && _pauseHead==AnimationTrigger.LookInPlaceForFacesHeadMovePause &&
                new[]{_bodySpeed,_tiltSpeed,_headSpeed,_chance}.All(float.IsFinite) &&
                _bodyMin.Concat(_bodyMax).Concat(_headMin).Concat(_headMax).All(float.IsFinite) &&
                !p.TryGetProperty("motionProfile",out _);
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {ReportMissing("FindFaces_socialize changed parameters unavailable: "+e.Message);}
        if(_robot.HasExternalInterface)
        {
            _treads=(from,to)=>
            {
                using var owner=_robot.Engine.EnterSynchronousOwner();
                if(to!=OffTreadsState.OnTreads || Volatile.Read(ref _retired)!=0 || !ReferenceEquals(_robot.Engine.Robot,_robot))return;
                if(!Available){ReportMissing("FindFaces_socialize on-treads center needs actual pose/runtime input");return;}
                _center=Yaw();
            };
            _m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged+=_treads;
        }
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long v)
    {
        if(Volatile.Read(ref _retired)!=0 || Interlocked.Read(ref _generation)!=v || !ReferenceEquals(_robot.Engine.Robot,_robot))return false;
        if(!Available){ReportMissing("FindFaces_socialize live action continuation needs unavailable pose/runtime input");return false;}
        return true;
    }
    protected override bool HostStopContinuationCurrent(long v)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(v+1);
    protected override bool HostResumeContinuationCurrent(long v)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==v||Interlocked.Read(ref _generation)==unchecked(v+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available){MarkReadinessUnavailable();ReportMissing("FindFaces_socialize actual owner/parameters unavailable");return false;}
        return IsRunnableBase(context);
    }
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    private float Yaw(){using var mutation=_m.World.EnterPoseMutation();using var flat=_robot.GetCheckedMainPose().GetWithRespectToRoot();return (float)flat.AngleAroundZ;}
    protected override void OnStart()
    {
        if(!Available){MarkReadinessUnavailable();InitFailed=true;ReportMissing("FindFaces_socialize actual input unavailable");return;}
        _accumulated=0;_endpoints=0;_center=Yaw();
        long g=CaptureHostExecutionVersion();
        byte direction=(byte)(_robot.Engine.ContextRandom.RandDbl(1)>_chance?1:0);
        if(!HostExecutionVersionCurrent(g))return;_direction=direction;
        StartActing(_m.Robot.Motion.CreateNativeLiftChild(32f,5f),_=>LookUp(g),()=>HostExecutionVersionCurrent(g));
    }
    private const double Deg=0.017453292519943295;
    private static float Degrees(float radians)=>radians*BitConverter.UInt32BitsToSingle(0x42652EE1);
    private NativePanTiltRunner Pan(float body,float head,float panSpeed,float tilt)
    {
        var p=new PanAndTiltAction{PanAngleRad=new Radians(body).Value,HeadAngleRad=new Radians(head).Value,PanIsAbsolute=true,HeadIsAbsolute=true};
        float scale=BitConverter.UInt32BitsToSingle(0x3C8EFA35);p.SetMaxPanSpeed(panSpeed*scale);p.SetMaxTiltSpeed(tilt*scale);return new NativePanTiltRunner(_m,p);
    }
    private NativePanTiltRunner? BodyTurn(int state,int direction,long g)
    {
        double b=_robot.Engine.ContextRandom.RandDblInRange(_bodyMin[state],_bodyMax[state]);
        if(!HostExecutionVersionCurrent(g))return null;
        float delta=(float)((direction==0?-b:b)*Deg);float head=(float)(_robot.Engine.ContextRandom.RandDblInRange(_headMin[state],_headMax[state])*Deg);
        if(!HostExecutionVersionCurrent(g))return null;
        return Pan(new Radians(_baseline+delta).Value,head,_bodySpeed,_tiltSpeed);
    }
    private NativePanTiltRunner? HeadTurn(float min,float max,float hmin,float hmax,float baseDegrees,long g,float panSpeed,float tiltSpeed)
    {
        int sign=_robot.Engine.ContextRandom.RandInt(2)==0?-1:1;
        if(!HostExecutionVersionCurrent(g))return null;
        double b=_robot.Engine.ContextRandom.RandDblInRange(min,max);
        if(!HostExecutionVersionCurrent(g))return null;
        float body=(float)(((double)baseDegrees+sign*b)*Deg);
        float head=(float)(_robot.Engine.ContextRandom.RandDblInRange(hmin,hmax)*Deg);
        if(!HostExecutionVersionCurrent(g))return null;
        return Pan(body,head,panSpeed,tiltSpeed);
    }
    private void Run(ActionRunner? action,Action next,long g)
    {
        if(action is null)return;
        if(!HostExecutionVersionCurrent(g)){action.RetireNeverAdmittedTree();return;}
        StartActing(action,_=>{if(HostExecutionVersionCurrent(g))next();},()=>HostExecutionVersionCurrent(g));
    }
    private void LookUp(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=0;
        Run(HeadTurn(1,6,20,40,Degrees(Yaw()),g,150f,90f),()=>AfterLookUp(g),g);
    }
    private void AfterLookUp(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        uint imageTimestamp=_m.Robot.CameraSettings.LastProcessedImageTimestamp;
        if(!_robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) || origin is null)
        {ReportMissing("FindFaces_socialize cached-face current origin unavailable");return;}
        using var pose=new Pose3d("");uint stamp=_m.Vision.Faces.CopyNativeLastFaceTimestamp(origin,false,pose);
        if(!HostExecutionVersionCurrent(g))return;
        if(stamp!=0 && (_maxAge==0 || imageTimestamp<=unchecked(stamp+_maxAge)))
            Run(new NativeLastFaceRunner(_m,_robot,0),()=>Begin(g),g);
        else Begin(g);
    }
    private void Begin(long g){if(!HostExecutionVersionCurrent(g))return;_center=Yaw();S1(g);}
    private TriggerLiftSafeAnimationAction Pause(AnimationTrigger trigger)=>new(_m.Robot,new(trigger,1,true,0,60f,false));
    private void S1(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=1;_baseline=Yaw();Run(BodyTurn(1,_direction==0?1:0,g),()=>S2(g),g);}
    private void S2(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=2;Run(Pause(_pauseBody),()=>S3(g),g);}
    private void S3(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=3;var action=BodyTurn(3,_direction,g);if(action is null)return;
        byte count=unchecked((byte)_robot.Engine.ContextRandom.RandIntInRange(_changesMin,_changesMax));
        if(!HostExecutionVersionCurrent(g)){action.RetireNeverAdmittedTree();return;}_total=_remaining=count;
        Run(action,()=>{if(_remaining==0)S6(g);else S4(g);},g);
    }
    private void S4(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=4;if(_remaining==_total)_headBaseline=Yaw();--_remaining;
        Run(Pause(_pauseHead),()=>Run(HeadTurn(_bodyMin[4],_bodyMax[4],_headMin[4],_headMax[4],Degrees(_headBaseline),g,_bodySpeed,_headSpeed),()=>{if(_remaining!=0)S4(g);else S5(g);},g),g);
    }
    private void S5(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=5;Run(HeadTurn(_bodyMin[5],_bodyMax[5],_headMin[5],_headMax[5],Degrees(_headBaseline),g,_bodySpeed,_headSpeed),()=>S6(g),g);}
    private void S6(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=6;Run(BodyTurn(6,_direction,g),()=>S7(g),g);}
    private void S7(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=7;_iterations=unchecked(_iterations+1);
        float yaw=Yaw();_accumulated+=new Radians(yaw-_baseline).Value;
        float relative=new Radians(yaw-_center).Value;
        if(Degrees(relative)*(_direction==0?-1f:1f)>=_focus*0.5f){_endpoints=unchecked((byte)(_endpoints+1));_direction=(byte)(_direction==0?1:0);}
        S1(g);
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);if(_treads is not null)_m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged-=_treads;RetireActionCompletionOwner();}
}
