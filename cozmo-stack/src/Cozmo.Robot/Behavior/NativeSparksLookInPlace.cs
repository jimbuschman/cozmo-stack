using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured SparksLookInPlace and its inherited scan-in-place action core.
internal sealed class NativeSparksLookInPlace : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly float[] _bodyMin=new float[7],_bodyMax=new float[7],_headMin=new float[7],_headMax=new float[7];
    private readonly float _bodySpeed,_tiltSpeed,_headSpeed,_chance,_focus;
    private readonly float _waitMin,_waitMax;
    private readonly bool _valid;
    private readonly Action<OffTreadsState,OffTreadsState>? _treads;
    private float _center,_baseline,_accumulated;
    private byte _direction,_endpoints;
    private uint _iterations;
    private long _generation;
    private int _retired;
    public uint SearchIterations=>_iterations;
    internal int NativeState {get;private set;}
    internal bool ParametersAvailable=>_valid;
    private bool Available=>_valid && NativeScoringAvailable && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} r && ReferenceEquals(r.Manipulation,_m) && r.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this) && ReferenceEquals(_m.World,_m.Robot.LocalizationWorld) &&
        _robot.CarryingOwner is not null && _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null && _robot.MainPose.Node is not null;
    internal NativeSparksLookInPlace(BehaviorFactoryContext source,JsonElement root):base("SparksLookInPlace","ExploreLookAroundInPlace")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        try
        {
            var p=root.GetProperty("params");float F(string key)=>p.GetProperty(key).GetSingle();
            _bodySpeed=F("sx_BodyTurnSpeed_degPerSec");_tiltSpeed=F("sxt_HeadTurnSpeed_degPerSec");_headSpeed=F("sxh_HeadTurnSpeed_degPerSec");
            _chance=F("s0_MainTurnCWChance");_focus=F("behavior_AngleOfFocus_deg");
            foreach(int n in new[]{1,3,6})
            {
                _bodyMin[n]=F($"s{n}_BodyAngleRangeMin_deg");_bodyMax[n]=F($"s{n}_BodyAngleRangeMax_deg");
                _headMin[n]=F($"s{n}_HeadAngleRangeMin_deg");_headMax[n]=F($"s{n}_HeadAngleRangeMax_deg");
            }
            _waitMin=F("s2_WaitMin_sec");_waitMax=F("s2_WaitMax_sec");
            _valid=p.GetProperty("behavior_ShouldResetTurnDirection").GetBoolean() && p.GetProperty("behavior_ResetBodyFacingOnStart").GetBoolean() &&
                p.GetProperty("behavior_ShouldLowerLift").GetBoolean() && p.GetProperty("behavior_CanCarryCube").GetBoolean() &&
                p.GetProperty("behavior_RecentLocationsMax").GetInt32()==0 && _focus==0f &&
                p.GetProperty("s4_HeadAngleChangesMin").GetInt32()==0 && p.GetProperty("s4_HeadAngleChangesMax").GetInt32()==0 &&
                p.GetProperty("s2_WaitAnimTrigger").GetString()=="" && !p.TryGetProperty("motionProfile",out _) &&
                new[]{_bodySpeed,_tiltSpeed,_headSpeed,_chance,_waitMin,_waitMax}.All(float.IsFinite) &&
                _bodyMin.Concat(_bodyMax).Concat(_headMin).Concat(_headMax).All(float.IsFinite);
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {ReportMissing("SparksLookInPlace changed parameters unavailable: "+e.Message);}
        if(_robot.HasExternalInterface)
        {
            _treads=(from,to)=>
            {
                using var owner=_robot.Engine.EnterSynchronousOwner();
                if(to!=OffTreadsState.OnTreads || Volatile.Read(ref _retired)!=0 || !ReferenceEquals(_robot.Engine.Robot,_robot))return;
                if(!Available){ReportMissing("SparksLookInPlace on-treads center needs actual pose/runtime input");return;}
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
        if(!Available){ReportMissing("SparksLookInPlace live action continuation needs unavailable pose/runtime input");return false;}
        return true;
    }
    protected override bool HostStopContinuationCurrent(long v)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(v+1);
    protected override bool HostResumeContinuationCurrent(long v)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==v||Interlocked.Read(ref _generation)==unchecked(v+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available){MarkReadinessUnavailable();ReportMissing("SparksLookInPlace actual owner/parameters unavailable");return false;}
        return IsRunnableBase(context);
    }
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    private float Yaw(){using var mutation=_m.World.EnterPoseMutation();return (float)_robot.GetCheckedMainPose().AngleAroundZ;}
    protected override void OnStart()
    {
        if(!Available){MarkReadinessUnavailable();InitFailed=true;ReportMissing("SparksLookInPlace actual input unavailable");return;}
        _accumulated=0;_endpoints=0;_center=Yaw();
        long g=CaptureHostExecutionVersion();
        byte direction=(byte)(_robot.Engine.ContextRandom.RandDbl(1)>_chance?1:0);
        if(!HostExecutionVersionCurrent(g))return;_direction=direction;
        if((_robot.CarryingOwner!.CarriedObjectId??uint.MaxValue)==uint.MaxValue)
            StartActing(_m.Robot.Motion.CreateNativeLiftChild(32f,5f),_=>S1(g),()=>HostExecutionVersionCurrent(g));
        else S1(g);
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
    private void Run(ActionRunner? action,Action next,long g)
    {
        if(action is null)return;
        if(!HostExecutionVersionCurrent(g)){action.RetireNeverAdmittedTree();return;}
        StartActing(action,_=>{if(HostExecutionVersionCurrent(g))next();},()=>HostExecutionVersionCurrent(g));
    }
    private void S1(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=1;_baseline=Yaw();Run(BodyTurn(1,_direction==0?1:0,g),()=>S2(g),g);}
    private void S2(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=2;
        float duration=(float)_robot.Engine.ContextRandom.RandDblInRange(_waitMin,_waitMax);
        if(!HostExecutionVersionCurrent(g))return;
        Run(new NativeTimedWaitRunner(_m,duration),()=>S3(g),g);
    }
    private void S3(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=3;var action=BodyTurn(3,_direction,g);if(action is null)return;
        _robot.Engine.ContextRandom.RandIntInRange(0,0);
        if(!HostExecutionVersionCurrent(g)){action.RetireNeverAdmittedTree();return;}
        Run(action,()=>S6(g),g);
    }
    private void S6(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=6;Run(BodyTurn(6,_direction,g),()=>S7(g),g);}
    private void S7(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=7;_iterations=unchecked(_iterations+1);
        _accumulated+=new Radians(Yaw()-_baseline).Value;
        if(MathF.Abs(_accumulated)<BitConverter.UInt32BitsToSingle(0x40C90FDB))S1(g);
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);if(_treads is not null)_m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged-=_treads;RetireActionCompletionOwner();}
}
