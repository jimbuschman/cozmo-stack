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
    private bool _publicRequest;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    private NativeVerifyObjectRunner? _verification;
    protected override string ActionName=>"TurnTowardsObject";
    internal NativeTurnTowardsObjectRunner(ManipulationSystem m,uint id,short code,float maximum,bool verify,bool track)
        :base(m,Pose3d.Identity,maximum,0x2C)
    {_id=id;_code=code;_verify=verify;_track=track;}
    internal void ConfigurePublicRequest(EngineRobot owner, Func<bool> current)
    {
        _publicRequest = true;
        ScopedOwner = owner;
        ScopedParentCurrent = current;
        Parameters.SetMaxPanSpeed(0); Parameters.SetPanAccel(0); Parameters.SetPanTolerance(0);
        Parameters.SetMaxTiltSpeed(0); Parameters.SetTiltAccel(0); Parameters.SetTiltTolerance(0);
    }
    public override byte? GetCompletionVariant() => _publicRequest ? (byte)0 : base.GetCompletionVariant();
    public override ObjectInteractionCompletion? GetObjectInteractionCompletion()
    {
        if (!_publicRequest) return base.GetObjectInteractionCompletion();
        Behavior.SteppedBehavior.ReportMissing("M1-034/M8-015 TurnTowardsObjectAction.GetCompletionUnion 0x0054B020–0x0054B036: native interaction words are undefined; retain defined metadata only.");
        return new ObjectInteractionCompletion(_id, 0, 0, 0, 0, 1, false, false);
    }
    internal void SetTolerances(float pan,float tilt)
    {Parameters.SetPanTolerance(pan);Parameters.SetTiltTolerance(tilt);}
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_verification is not null)_verification.Watcher=value;}}
    public override int Init()
    {
        if(!ScopedCurrent)return unchecked((int)EngineActionResult.Cancelled);
        using var mutation=M.World.EnterPoseMutation();
        var target=M.World.GetLocatedObjectByIdHelper(_id);
        if(target is null)
        {
            if (_publicRequest) M.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "TurnTowardsObjectAction.Init.ObjectNotFound", $"Object with ID={unchecked((int)_id)} no longer exists in the world.");
            return ScopedCurrent && !DestructionObserved ? 0x03000004 : unchecked((int)EngineActionResult.Cancelled);
        }
        var main=(ScopedOwner??M.Robot.Engine.Robot!).GetCheckedMainPose();
        using var selected=new Pose3d("");using var relative=new Pose3d("");
        float best=float.MaxValue;bool found=false;
        foreach(var marker in target.Markers.ToArray())
        {
            if(_code!=AnyCode&&unchecked((short)marker.Code)!=_code)continue;
            if(!marker.PoseOnObject.TryWithRespectTo(main,relative))
            {
                if(_code!=AnyCode)
                {
                    string custom=_id==uint.MaxValue?"custom ":"";
                    M.Robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"TurnTowardsObjectAction.Init.MarkerOriginProblem",$"Could not get pose of marker with code {_code} of {custom}object {unchecked((int)target.ObjectId)} w.r.t. robot pose.");
                    if(DestructionObserved || !ScopedCurrent)return unchecked((int)EngineActionResult.Cancelled);
                    Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                    return 0x03000005; // 0x0054A3D8, 0x0054A65C..0x0054A6B8
                }
                if (!_publicRequest) Behavior.SteppedBehavior.ReportMissing("TurnTowardsObject marker pose conversion failed0054A36C; marker skipped");
                continue;
            }
            var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
            float distance=x*x;distance=distance+y*y;
            if(_code!=AnyCode){distance=distance+z*z;distance=MathF.Sqrt(distance);}
            if(!(distance<best))continue;
            best=distance;selected.Assign(relative);found=true;
        }
        if(!found)
        {
            if (_publicRequest) M.Robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "TurnTowardsObjectAction.Init.NoValidPose", $"Could not get a valid closest marker pose of object {(int)target.Type}");
            if (!ScopedCurrent || DestructionObserved) return unchecked((int)EngineActionResult.Cancelled);
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return 0x03000002;
        }
        AssignGoal(selected);
        if(!ScopedCurrent)return unchecked((int)EngineActionResult.Cancelled);
        return base.Init();
    }
    public override uint CheckIfDone()
    {
        if(!_finished)
        {
            uint result=base.CheckIfDone();if(result!=0)return result;
            if(DestructionObserved || !ScopedCurrent)return EngineActionResult.Cancelled;
            if(_refine){Reset(false);_refine=false;Parameters.SetNativePanTolerance(BitConverter.UInt32BitsToSingle(0x3DB2B8C2));return EngineActionResult.Running;}
            _finished=true;
            if(_track)throw new NotSupportedException("MISSING native TurnObject TrackObject recipient0054A9");
            if(!_verify)return 0;
            _verification=new(M,_id,_code){SuppressTrackLocking=true,Watcher=Watcher,ScopedOwner=ScopedOwner,ScopedParentCurrent=()=>ScopedCurrent && !DestructionObserved};
            uint first=_verification.Update();if(first!=0)return first;
            if(DestructionObserved || !ScopedCurrent)return EngineActionResult.Cancelled;
        }
        return _verification?.Update()??0; //source immediate first-success joins the retained child update again.
    }
    public override void Prep(){_verification?.Prep();base.Prep();}
    internal override bool RetireNeverAdmittedTree()
    {
        if (!base.RetireNeverAdmittedTree()) return false;
        if (_publicRequest) GoalPose.Dispose();
        return true;
    }
    public override void WatcherEnding()
    {
        _verification?.Prep(); _verification?.WatcherEnding(); _verification = null;
        base.WatcherEnding();
        if (AiNativeEnded is { } ended && ScopedOwner is { } owner)
            ended.TrySetResult((State, owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot, owner)));
    }
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
        if(!ScopedCurrent)return unchecked((int)EngineActionResult.Cancelled);
        _parallel?.Prep();_parallel?.WatcherEnding();
        _parallel=new CompoundActionParallel(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
        _parallel.AddAction(M.Robot.Motion.CreateNativeLiftChild(CozmoMotion.OutOfFovHeightMm,5f,ScopedOwner,()=>ScopedCurrent && !DestructionObserved),false,false);
        if(DestructionObserved||!ScopedCurrent)return unchecked((int)EngineActionResult.Cancelled);
        _parallel.AddAction(new NativeWaitForImagesRunner(M,_images,(VisionMode)1,0){ScopedOwner=ScopedOwner,ScopedParentCurrent=()=>ScopedCurrent && !DestructionObserved},false,false);
        if(DestructionObserved||!ScopedCurrent)return unchecked((int)EngineActionResult.Cancelled);
        _seen=false;_markerAccepted=_code==NativeTurnTowardsObjectRunner.AnyCode;
        if(M.Robot.Engine.Robot?.HasExternalInterface!=true)
            throw new NotSupportedException("M1-034 VerifyObject native external owner absent at44 subscription");
        var next=M.Robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(ScopedCurrent&&!_seen&&p is NativeObservedObject o&&o.ObjectId==_id)_seen=true;});
        if(DestructionObserved||!ScopedCurrent){next.Dispose();return unchecked((int)EngineActionResult.Cancelled);}
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
    {if(!ScopedCurrent)return EngineActionResult.Cancelled;if(HaveSeen())return 0;uint result=_parallel!.Update();if(DestructionObserved||!ScopedCurrent)return EngineActionResult.Cancelled;return result==EngineActionResult.Running?EngineActionResult.Running:0x0300001D;}
    public override void Prep(){_parallel?.Prep();base.Prep();}
    public override void WatcherEnding()
    {_parallel?.Prep();_observed?.Dispose();_observed=null;_parallel?.WatcherEnding();_parallel=null;base.WatcherEnding();}
}
