using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Vision;

//00549DC0..A710: owned source TurnObject; same TurnPose core, no second wrapper tag.
internal sealed class NativeTurnTowardsObjectRunner:NativeTurnTowardsPoseRunner
{
    internal const short AnyCode=32767;
    private readonly uint _id;
    private readonly short _code;
    private readonly bool _verify,_track;
    private bool _refine=true,_finished;
    private NativeVerifyObjectRunner? _verification;
    protected override string ActionName=>"TurnTowardsObject";
    internal NativeTurnTowardsObjectRunner(ManipulationSystem m,uint id,short code,float maximum,bool verify,bool track)
        :base(m,Pose3d.Identity,maximum,0x2C)
    {_id=id;_code=code;_verify=verify;_track=track;}
    internal void SetTolerances(float pan,float tilt)
    {Parameters.SetPanTolerance(pan);Parameters.SetTiltTolerance(tilt);}
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_verification is not null)_verification.Watcher=value;}}
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        var target=M.World.GetLocatedObjectByIdHelper(_id);
        if(target is null)return 0x03000004;
        var main=M.Robot.Engine.Robot!.GetCheckedMainPose();
        using var selected=new Pose3d("");using var relative=new Pose3d("");
        float best=float.MaxValue;bool found=false;
        foreach(var marker in target.Markers.ToArray())
        {
            if(_code!=AnyCode&&unchecked((short)marker.Code)!=_code)continue;
            if(!marker.PoseOnObject.TryWithRespectTo(main,relative))
            {Behavior.SteppedBehavior.ReportMissing("TurnTowardsObject marker pose conversion failed0054A36C; marker skipped");continue;}
            var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
            float distance=x*x;distance=distance+y*y;
            if(_code!=AnyCode){distance=distance+z*z;distance=MathF.Sqrt(distance);}
            if(!(distance<best))continue;
            best=distance;selected.Assign(relative);found=true;
        }
        if(!found){Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return 0x03000002;}
        AssignGoal(selected);
        return base.Init();
    }
    public override uint CheckIfDone()
    {
        if(!_finished)
        {
            uint result=base.CheckIfDone();if(result!=0)return result;
            if(_refine){Reset(false);_refine=false;Parameters.SetNativePanTolerance(BitConverter.UInt32BitsToSingle(0x3DB2B8C2));return EngineActionResult.Running;}
            _finished=true;
            if(_track)throw new NotSupportedException("MISSING native TurnObject TrackObject recipient0054A9");
            if(!_verify)return 0;
            _verification=new(M,_id,_code){SuppressTrackLocking=true,Watcher=Watcher};
            uint first=_verification.Update();if(first!=0)return first;
        }
        return _verification?.Update()??0; //source immediate first-success joins the retained child update again.
    }
    public override void Prep(){_verification?.Prep();base.Prep();}
    public override void WatcherEnding(){_verification?.Prep();_verification?.WatcherEnding();_verification=null;base.WatcherEnding();}
}

//00568... IVisuallyVerifyObject: real retained44 + lift/Wait43 parallel recipient.
internal sealed class NativeVerifyObjectRunner:NativeVisionRunner
{
    private readonly uint _id;
    private readonly short _code;
    private CompoundActionParallel? _parallel;
    private RetainedSubscription? _observed;
    private bool _seen,_markerAccepted;
    private readonly int _images;
    protected override string ActionName=>"VisuallyVerifyObject";
    internal NativeVerifyObjectRunner(ManipulationSystem m,uint id,short code,int images=10):base(m,0x2E,1){_id=id;_code=code;_images=images;}
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_parallel is not null)_parallel.Watcher=value;}}
    public override int Init()
    {
        _parallel?.Prep();_parallel?.WatcherEnding();
        _parallel=new CompoundActionParallel(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
        _parallel.AddAction(M.Robot.Motion.CreateNativeLiftChild(CozmoMotion.OutOfFovHeightMm,5f),false,false);
        _parallel.AddAction(new NativeWaitForImagesRunner(M,_images,(VisionMode)1,0),false,false);
        _seen=false;_markerAccepted=_code==NativeTurnTowardsObjectRunner.AnyCode;
        if(M.Robot.Engine.Robot?.HasExternalInterface!=true)
            throw new NotSupportedException("M1-034 VerifyObject native external owner absent at44 subscription");
        var next=M.Robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(!_seen&&p is NativeObservedObject o&&o.ObjectId==_id)_seen=true;});
        var old=_observed;_observed=next;old?.Dispose();
        return 0;
    }
    internal bool HaveSeen()
    {
        if(!_seen)return false;if(_markerAccepted)return true;
        using var mutation=M.World.EnterPoseMutation();
        if(M.World.GetLocatedObjectByIdHelper(_id) is not {} target)return false;
        if(NativeObservedMarkers.Get(target,target.LastObservedTimestamp).Any(m=>unchecked((short)m.Code)==_code))_markerAccepted=true;
        return _markerAccepted;
    }
    public override uint CheckIfDone()
    {if(HaveSeen())return 0;return _parallel!.Update()==EngineActionResult.Running?EngineActionResult.Running:0x0300001D;}
    public override void Prep(){_parallel?.Prep();base.Prep();}
    public override void WatcherEnding()
    {_parallel?.Prep();_observed?.Dispose();_observed=null;_parallel?.WatcherEnding();_parallel=null;base.WatcherEnding();}
}
