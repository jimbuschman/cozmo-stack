using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured SparksLookInPlace and Hiking_LookInPlace360, both using the 0x005E1D80 scan core.
internal sealed class NativeSparksLookInPlace : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly float[] _bodyMin=new float[7],_bodyMax=new float[7],_headMin=new float[7],_headMax=new float[7];
    private readonly float _bodySpeed,_tiltSpeed,_headSpeed,_chance,_focus;
    private readonly float _waitMin,_waitMax,_headWaitMin,_headWaitMax,_recentDistance;
    private readonly bool _canCarry;
    private readonly byte _changesMin,_changesMax,_recentMax;
    private readonly AnimationTrigger _bodyPause,_headPause;
    private readonly Queue<Pose3d> _recent = new();
    private readonly bool _valid;
    private readonly Action<OffTreadsState,OffTreadsState>? _treads;
    private float _center,_baseline,_headBaseline,_accumulated;
    private byte _direction,_remaining,_total;
    private uint _iterations;
    private long _generation;
    private int _retired;
    public uint SearchIterations=>_iterations;
    internal int NativeState {get;private set;}
    internal bool ParametersAvailable=>_valid;
    private bool Available=>_valid && NativeScoringAvailable && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} r && ReferenceEquals(r.Manipulation,_m) && r.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this) && ReferenceEquals(_m.World,_m.Robot.LocalizationWorld) &&
        _robot.CarryingOwner is not null && _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null && _robot.MainPose.Node is not null;
    internal NativeSparksLookInPlace(BehaviorFactoryContext source,JsonElement root):base(BehaviorConfigLoader.ExtractIdFromConfig(root).ToString(),"ExploreLookAroundInPlace")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        try
        {
            var p=root.GetProperty("params");float F(string key)=>p.GetProperty(key).GetSingle();
            _bodySpeed=F("sx_BodyTurnSpeed_degPerSec");_tiltSpeed=F("sxt_HeadTurnSpeed_degPerSec");_headSpeed=F("sxh_HeadTurnSpeed_degPerSec");
            _chance=F("s0_MainTurnCWChance");_focus=F("behavior_AngleOfFocus_deg");
            foreach(int n in new[]{1,3,4,5,6})
            {
                string bodyRange = n is 4 or 5 ? $"s{n}_BodyAngleRelativeRange" : $"s{n}_BodyAngleRange";
                _bodyMin[n]=F(bodyRange+"Min_deg");_bodyMax[n]=F(bodyRange+"Max_deg");
                _headMin[n]=F($"s{n}_HeadAngleRangeMin_deg");_headMax[n]=F($"s{n}_HeadAngleRangeMax_deg");
            }
            _waitMin=F("s2_WaitMin_sec");_waitMax=F("s2_WaitMax_sec");
            _headWaitMin=F("s4_WaitBetweenChangesMin_sec");_headWaitMax=F("s4_WaitBetweenChangesMax_sec");
            _canCarry=p.GetProperty("behavior_CanCarryCube").GetBoolean();
            _recentMax=p.GetProperty("behavior_RecentLocationsMax").GetByte();
            _recentDistance=F("behavior_DistanceFromRecentLocationMin_mm");
            _changesMin=p.GetProperty("s4_HeadAngleChangesMin").GetByte();
            _changesMax=p.GetProperty("s4_HeadAngleChangesMax").GetByte();
            AnimationTrigger PauseTrigger(string key)
            {
                string name=p.GetProperty(key).GetString()!;
                return name.Length==0 ? (AnimationTrigger)0x23F : Enum.Parse<AnimationTrigger>(name);
            }
            _bodyPause=PauseTrigger("s2_WaitAnimTrigger");
            _headPause=PauseTrigger("s4_WaitAnimTrigger");
            bool hiking=Id==nameof(BehaviorID.Hiking_LookInPlace360);
            bool supportedProfile=hiking
                ? !_canCarry && _recentMax==5 && _changesMin==2 && _changesMax==4 &&
                  _bodyPause==AnimationTrigger.LookInPlaceForFacesBodyPause &&
                  _headPause==AnimationTrigger.LookInPlaceForFacesHeadMovePause
                : _canCarry && _recentMax==0 && _changesMin==0 && _changesMax==0 &&
                  _bodyPause==(AnimationTrigger)0x23F && _headPause==(AnimationTrigger)0x23F;
            _valid=supportedProfile && p.GetProperty("behavior_ShouldResetTurnDirection").GetBoolean() &&
                p.GetProperty("behavior_ResetBodyFacingOnStart").GetBoolean() &&
                p.GetProperty("behavior_ShouldLowerLift").GetBoolean() && _focus==0f &&
                !p.TryGetProperty("motionProfile",out _) &&
                new[]{_bodySpeed,_tiltSpeed,_headSpeed,_chance,_waitMin,_waitMax,_headWaitMin,_headWaitMax,_recentDistance}.All(float.IsFinite) &&
                _bodyMin.Concat(_bodyMax).Concat(_headMin).Concat(_headMax).All(float.IsFinite);
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {ReportMissing($"{Id} changed parameters unavailable: {e.Message}");}
        if(_robot.HasExternalInterface)
        {
            _treads=(from,to)=>
            {
                using var owner=_robot.Engine.EnterSynchronousOwner();
                if(to!=OffTreadsState.OnTreads || Volatile.Read(ref _retired)!=0 || !ReferenceEquals(_robot.Engine.Robot,_robot))return;
                if(!Available){ReportMissing($"{Id} on-treads center needs actual pose/runtime input");return;}
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
        if(!Available){ReportMissing($"{Id} live action continuation needs unavailable pose/runtime input");return false;}
        return true;
    }
    protected override bool HostStopContinuationCurrent(long v)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(v+1);
    protected override bool HostResumeContinuationCurrent(long v)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==v||Interlocked.Read(ref _generation)==unchecked(v+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!Available){MarkReadinessUnavailable();ReportMissing($"{Id} actual owner/parameters unavailable");return false;}
        if (!IsRunnableBase(context)) return false;
        // 0x005E25F4..0x005E2648 compares relative XYZ in f32. Other roots fail the pose
        // conversion and do not disqualify the location. Equality is admitted.
        if (_recent.Count==0) return true;
        using var mutation=_m.World.EnterPoseMutation();
        using var relative=new Pose3d();
        var current=_robot.GetCheckedMainPose();
        foreach(var pose in _recent)
        {
            if(!Pose3d.GetWithRespectTo(pose,current,relative))continue;
            var t=relative.Translation;
            float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
            if(x*x+y*y+z*z<_recentDistance*_recentDistance)return false;
        }
        return true;
    }
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>_canCarry; // 0x005C1D76 reads config +0x120.
    private float Yaw(){using var mutation=_m.World.EnterPoseMutation();return (float)_robot.GetCheckedMainPose().AngleAroundZ;}
    protected override void OnStart()
    {
        if(!Available){MarkReadinessUnavailable();InitFailed=true;ReportMissing($"{Id} actual input unavailable");return;}
        _accumulated=0;_center=Yaw();
        long g=CaptureHostExecutionVersion();
        byte direction=(byte)(_robot.Engine.ContextRandom.RandDbl(1)>_chance?1:0);
        if(!HostExecutionVersionCurrent(g))return;_direction=direction;
        if(!_canCarry || (_robot.CarryingOwner!.CarriedObjectId??uint.MaxValue)==uint.MaxValue)
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
    private void Pause(AnimationTrigger trigger,float min,float max,Action next,long g)
    {
        // S2 0x005E2B78..0x005E2BE8 and S4 0x005E2ECC..0x005E2FCC select
        // one lift-safe trigger or one randomized wait. The trigger consumes no wait RNG.
        if(trigger!=(AnimationTrigger)0x23F)
            Run(new TriggerLiftSafeAnimationAction(_m.Robot,new(trigger,1,true,0,60f,false)),next,g);
        else
        {
            float duration=(float)_robot.Engine.ContextRandom.RandDblInRange(min,max);
            if(HostExecutionVersionCurrent(g))Run(new NativeTimedWaitRunner(_m,duration),next,g);
        }
    }
    private void S2(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        NativeState=2;
        Pause(_bodyPause,_waitMin,_waitMax,()=>S3(g),g);
    }
    private void S3(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        NativeState=3;
        var action=BodyTurn(3,_direction,g);
        if(action is null)return;
        // 0x005E2CA6..0x005E2CC8 draws the count after constructing the body action.
        byte count=unchecked((byte)_robot.Engine.ContextRandom.RandIntInRange(_changesMin,_changesMax));
        if(!HostExecutionVersionCurrent(g)){action.RetireNeverAdmittedTree();return;}
        _total=_remaining=count;
        Run(action,count==0?()=>S6(g):()=>S4(g),g);
    }
    private NativePanTiltRunner? HeadTurn(int state,long g)
    {
        // 0x005E32AA..0x005E337C draws sign, body magnitude and head angle in order.
        int sign=_robot.Engine.ContextRandom.RandInt(2)==0?-1:1;
        if(!HostExecutionVersionCurrent(g))return null;
        double magnitude=_robot.Engine.ContextRandom.RandDblInRange(_bodyMin[state],_bodyMax[state]);
        if(!HostExecutionVersionCurrent(g))return null;
        float body=(float)(((double)Degrees(_headBaseline)+sign*magnitude)*Deg);
        float head=(float)(_robot.Engine.ContextRandom.RandDblInRange(_headMin[state],_headMax[state])*Deg);
        return HostExecutionVersionCurrent(g)?Pan(body,head,_bodySpeed,_headSpeed):null;
    }
    private void S4(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        NativeState=4;
        if(_remaining==_total)_headBaseline=Yaw();
        --_remaining;
        // 0x005E2E34..0x005E2E76 saves a shared heading and selects S4 or S5.
        // The pause callback at 0x005E3B98..0x005E3C5C queues the head turn.
        Action next=_remaining==0?()=>S5(g):()=>S4(g);
        Pause(_headPause,_headWaitMin,_headWaitMax,()=>Run(HeadTurn(4,g),next,g),g);
    }
    private void S5(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;
        NativeState=5;
        // 0x005E3212..0x005E3258 reuses the S4 heading with the S5 ranges.
        Run(HeadTurn(5,g),()=>S6(g),g);
    }
    private void S6(long g){if(!HostExecutionVersionCurrent(g))return;NativeState=6;Run(BodyTurn(6,_direction,g),()=>S7(g),g);}
    private void S7(long g)
    {
        if(!HostExecutionVersionCurrent(g))return;NativeState=7;_iterations=unchecked(_iterations+1);
        _accumulated+=new Radians(Yaw()-_baseline).Value;
        // Zero focus takes the full-scan branch at 0x005E3674..0x005E3724.
        if(MathF.Abs(_accumulated)<BitConverter.UInt32BitsToSingle(0x40C90FDB))
        {
            S1(g);
            return;
        }
        // 0x005E38DA..0x005E3920 replaces the oldest pose at capacity, then copies the current pose.
        if(_recentMax==0)return;
        using var mutation=_m.World.EnterPoseMutation();
        if(_recent.Count>=_recentMax)_recent.Dequeue().Dispose();
        _recent.Enqueue(new Pose3d(_robot.GetCheckedMainPose()));
    }
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    public void Dispose()
    {
        using var owner=_robot.Engine.EnterSynchronousOwner();
        if(Interlocked.Exchange(ref _retired,1)!=0)return;
        Interlocked.Increment(ref _generation);
        if(_treads is not null)_m.Robot.Sensors.ConfiguredBehaviorOffTreadsStateChanged-=_treads;
        RetireActionCompletionOwner();
        using var mutation=_m.World.EnterPoseMutation();
        while(_recent.TryDequeue(out var pose))pose.Dispose();
    }
}
