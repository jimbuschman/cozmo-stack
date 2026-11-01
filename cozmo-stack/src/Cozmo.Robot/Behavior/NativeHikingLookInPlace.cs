using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured Hiking_LookInPlaceForUnknown, native own lifecycle 0x005E482C..0x005E5A6C.
internal sealed class NativeHikingLookInPlace : SteppedBehavior, ILookInPlaceMemoryMapScoreState, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly bool _valid;
    private readonly byte[] _sectors=new byte[8];
    private readonly List<Pose3d> _previous=new();
    private float _yaw;
    private byte _completed;
    private long _generation;
    private int _retired;
    private bool _blocked;
    public byte CompletedSectorCount=>_completed;
    public byte PrioritySectorCount=>3;
    public float LowPriorityScoreReduction=>1f;
    internal IReadOnlyList<byte> Sectors=>_sectors;
    internal IReadOnlyList<Pose3d> PreviousLocations=>_previous;
    internal bool ParametersAvailable=>_valid;
    internal (Vec2 From,Vec2 To)? LastRay {get;private set;}
    private bool OwnerCurrent=>_valid && NativeScoringAvailable && Volatile.Read(ref _retired)==0 && ReferenceEquals(_robot.Engine.Robot,_robot) &&
        _robot.NativeBehaviorRuntime is {} runtime && ReferenceEquals(runtime.Manipulation,_m) && runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this) &&
        ReferenceEquals(_m.World,_m.Robot.LocalizationWorld) && _robot.CarryingOwner is not null && _robot.MainPose.Node is not null &&
        _robot.TryGetNativeOriginOwner(_robot.CurrentOriginId,out var origin) && origin?.Node is not null;
    private MemoryMap? CurrentMap()=>_m.World.Maps.CurrentOrigin==_robot.CurrentOriginId?_m.World.Maps.Current:null;
    internal NativeHikingLookInPlace(BehaviorFactoryContext source,JsonElement root):base("Hiking_LookInPlaceForUnknown","LookInPlaceMemoryMap")
    {
        _m=source.Manipulation!;_robot=source.NativeRobot!;
        try
        {
            var p=root.GetProperty("params");float F(string key)=>p.GetProperty(key).GetSingle();
            _valid=F("bodyTurnSpeed_degPerSec")==180f && F("headTurnSpeed_degPerSec")==90f &&
                F("bodyAngleFromSectorCenterRangeMin_deg")==0f && F("bodyAngleFromSectorCenterRangeMax_deg")==5f &&
                F("t1_headAngleAbsRangeMin_deg")==-15f && F("t1_headAngleAbsRangeMax_deg")==-10f &&
                F("t2_headAngleAbsRangeMin_deg")==-5f && F("t2_headAngleAbsRangeMax_deg")==5f &&
                p.GetProperty("prioritySectorCount").GetByte()==3 && F("lowPriorityScoreReduction")==1f &&
                F("distanceThresholdForLocations_mm")==30f && p.GetProperty("maxPreviousLocationCount").GetByte()==3 &&
                p.GetProperty("lookInPlaceAnimTrigger").GetString()=="HikingLookAround";
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {ReportMissing("Hiking_LookInPlaceForUnknown parameters unavailable: "+e.Message);}
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long value)
    {
        return Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==value && ReferenceEquals(_robot.Engine.Robot,_robot);
    }
    private bool InputsCurrent(long value)
    {
        if(!HostExecutionVersionCurrent(value))return false;
        if(OwnerCurrent && CurrentMap() is not null)return true;
        _blocked=true;MarkReadinessUnavailable();ReportMissing("M1-034/M13-031 Hiking_LookInPlaceForUnknown live current Robot/map input unavailable; scan held");return false;
    }
    protected override bool HostStopContinuationCurrent(long value)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(value+1);
    protected override bool HostResumeContinuationCurrent(long value)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==value || Interlocked.Read(ref _generation)==unchecked(value+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction || _blocked;
    protected override bool HoldUnavailableActionCompletion=>_blocked;
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        if(!_valid){MarkReadinessUnavailable();ReportMissing("M7-018 Hiking_LookInPlaceForUnknown changed parameters unavailable");return false;}
        if(!OwnerCurrent || CurrentMap() is null){MarkReadinessUnavailable();ReportMissing("M13-031 Hiking_LookInPlaceForUnknown actual Robot/map input unavailable");return false;}
        return base.IsRunnable(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        using var mutation=_m.World.EnterPoseMutation();
        if(!OwnerCurrent || CurrentMap() is null){MarkReadinessUnavailable();return false;}
        var current=_robot.GetCheckedMainPose();float limit=30f*30f+BitConverter.UInt32BitsToSingle(0x3727C5AC);
        foreach(var pose in _previous)
        {
            float distance=0;
            if(PlacementProducerGeometry.TryDistanceSquared(pose,current,ref distance) && !(limit<distance))return false;
        }
        return true;
    }
    protected override void OnStart()
    {
        if(!_valid){MarkReadinessUnavailable();InitFailed=true;ReportMissing("M7-018 Hiking_LookInPlaceForUnknown changed parameters unavailable");return;}
        if(!OwnerCurrent || CurrentMap() is null){MarkReadinessUnavailable();InitFailed=true;ReportMissing("M13-031 Hiking_LookInPlaceForUnknown Init map unavailable");return;}
        using(var mutation=_m.World.EnterPoseMutation())using(var root=_robot.GetCheckedMainPose().GetWithRespectToRoot())_yaw=(float)root.AngleAroundZ;
        Array.Clear(_sectors);_completed=0;_blocked=false;Find(0,7,0,CaptureHostExecutionVersion());
    }
    private float Center(int sector)=>(float)(sector*45f-22.5f)+_yaw*BitConverter.UInt32BitsToSingle(0x42652EE1);
    private bool? NeedsVisit(int sector,long generation)
    {
        if(!InputsCurrent(generation))return null;
        using var mutation=_m.World.EnterPoseMutation();var map=CurrentMap();if(map is null){_blocked=true;MarkReadinessUnavailable();ReportMissing("M1-034/M13-031 Hiking_LookInPlaceForUnknown skipped null current map ray");return null;}
        float angle=new Radians(Center(sector)*BitConverter.UInt32BitsToSingle(0x3C8EFA35)).Value;
        var direction=Rotation3d.FromAngleAxis(angle,new Vec3(0,0,1)).Rotate(new Vec3(1,0,0));
        using var root=_robot.GetCheckedMainPose().GetWithRespectToRoot();float x=(float)root.Translation.X,y=(float)root.Translation.Y;
        float dx=(float)direction.X,dy=(float)direction.Y;
        var from=new Vec2(x+dx*40f,y+dy*40f);var to=new Vec2(x+dx*190f,y+dy*190f);
        LastRay=(from,to); // observation of the actual ray sent to the native map primitive
        bool found=map.HasCollisionRayWithTypes(from,to,type=>type==MemoryMapContentType.Unknown);
        if(!InputsCurrent(generation) || !ReferenceEquals(CurrentMap(),map)){if(HostExecutionVersionCurrent(generation)){_blocked=true;MarkReadinessUnavailable();ReportMissing("M13-031 Hiking_LookInPlaceForUnknown current map changed during ray");}return null;}
        _sectors[sector]=(byte)(found?2:1);return found;
    }
    private void Find(int center,int left,int right,long generation)
    {
        if(!InputsCurrent(generation))return;
        while(true)
        {
            if(left<0)left=7;if(right>=8)right=0;
            if(left==right)
            {
                bool? last=NeedsVisit(left,generation);if(last is null)return;
                if(last.Value)Visit(left,left,right,generation);else FinishLocation(generation);
                return;
            }
            int ld=Math.Abs(left-center);if(ld>=5)ld=8-ld;
            int rd=Math.Abs(right-center);if(rd>=5)rd=8-rd;
            int selected;if(ld<rd){selected=left;left--;}else{selected=right;right++;}
            bool? visit=NeedsVisit(selected,generation);if(visit is null)return;
            if(visit.Value){Visit(selected,left,right,generation);return;}
        }
    }
    private NativePanTiltRunner Pan(float center,float bodyMin,float bodyMax,float headMin,float headMax,long generation)
    {
        const double deg=0.017453292519943295;
        double body=_robot.Engine.ContextRandom.RandDblInRange(bodyMin,bodyMax);
        float bodyAngle=(float)(((double)center+body)*deg);
        float headAngle=(float)(_robot.Engine.ContextRandom.RandDblInRange(headMin,headMax)*deg);
        var p=new PanAndTiltAction{PanAngleRad=new Radians(bodyAngle).Value,HeadAngleRad=new Radians(headAngle).Value,PanIsAbsolute=true,HeadIsAbsolute=true};
        float scale=BitConverter.UInt32BitsToSingle(0x3C8EFA35);p.SetMaxPanSpeed(180f*scale);p.SetMaxTiltSpeed(90f*scale);
        return new NativePanTiltRunner(_m,p);
    }
    private void Visit(int index,int left,int right,long generation)
    {
        if(!InputsCurrent(generation))return;
        float center=Center(index);var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);
        sequence.AddAction(Pan(center,0,5,-15,-10,generation),false,false);
        if(!InputsCurrent(generation)){sequence.RetireNeverAdmittedTree();return;}
        sequence.AddAction(_m.Robot.Motion.CreateNativeLiftChild(32f,5f),false,false);
        if(!InputsCurrent(generation)){sequence.RetireNeverAdmittedTree();return;}
        sequence.AddAction(new TriggerLiftSafeAnimationAction(_m.Robot,new(AnimationTrigger.HikingLookAround,1,true,0,60f,false)),false,false);
        if(!InputsCurrent(generation)){sequence.RetireNeverAdmittedTree();return;}
        sequence.AddAction(Pan(center,-0f,-5f,-5f,5f,generation),false,false);
        if(!InputsCurrent(generation)){sequence.RetireNeverAdmittedTree();return;}
        StartActing(sequence,_=>
        {
            if(!InputsCurrent(generation))return;
            _sectors[index]=3;_completed=unchecked((byte)(_completed+1));
            if(index==left && index==right)FinishLocation(generation);else Find(index,left,right,generation);
        },()=>InputsCurrent(generation));
    }
    private void FinishLocation(long generation)
    {
        if(!InputsCurrent(generation))return;
        using var mutation=_m.World.EnterPoseMutation();
        if(_previous.Count>=3){_previous[0].Dispose();_previous.RemoveAt(0);}
        _previous.Add(new Pose3d(_robot.GetCheckedMainPose()));
    }
    protected override void OnStop(BehaviorStopReason reason){_blocked=false;Interlocked.Increment(ref _generation);}
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);foreach(var pose in _previous)pose.Dispose();_previous.Clear();RetireActionCompletionOwner();}
}
