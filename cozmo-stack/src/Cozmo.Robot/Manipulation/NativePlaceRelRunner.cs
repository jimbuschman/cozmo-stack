using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// Proposed separate leaf owner: concrete PlaceRel Verify/completion P4-P7.
// Native Init metadata/shared recipients retain their existing M12 ownership.
internal sealed class NativePlaceRelRunner:NativeVisionRunner
{
    private readonly uint _originalTarget;
    internal PlaceRelObjectAction Action {get;}
    private IDisposable? _subscriptions;
    private CompoundActionSequential? _prepared;
    private ActionRunner? _verify;
    private short _marker=3;
    private uint _savedCarry=uint.MaxValue;
    private bool _sent,_latched,_light,_raised,_unavailable;
    private float _deadline=-1;
    private byte _squint;
    internal Action<NativeActionUnavailable>? Unavailable;
    protected override string ActionName=>"PlaceRelObject";
    internal uint SavedCarried=>_savedCarry;
    internal ActionRunner? VerifyChild=>_verify;
    internal bool Raised=>_raised;
    internal CompoundActionSequential? Prepared=>_prepared;
    internal NativePlaceRelRunner(ManipulationSystem m,uint target):base(m,0x15,7)
    {_originalTarget=target;Action=new(m,target,false,0,0,false,true);}
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_prepared is not null)_prepared.Watcher=value;if(_verify is not null)_verify.Watcher=value;}}
    public override uint Update()
    {
        if(_unavailable)return EngineActionResult.Running;
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;Unavailable?.Invoke(error);return EngineActionResult.Running;}
        catch(NativeConfigurationUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;Unavailable?.Invoke(new NativeActionUnavailable(error.Message));return EngineActionResult.Running;}
    }
    // fidelity: M12-025, M12-017, M12-034, M12-037
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();var owner=M.Robot.Engine.Robot!;
        _deadline=-1;_sent=false;owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        var target=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);if(target is null)return 0x03000004;
        var main=owner.GetCheckedMainPose();
        var pre=DockPreActionPoses.GetNative(new(target,PreActionType.PlaceRelative,true,DockActionBase.PreActionAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>M.NativeObstacles(main),M.World);
        if(pre.Result!=ActionResult.Success)return unchecked((int)pre.Result);
        uint carry=owner.CarryingOwner.CarriedObjectId??uint.MaxValue;if(carry==uint.MaxValue)return 0x03000011;
        var cache=owner.NativeBehaviorRuntime?.PickupTargets??throw new NativeActionUnavailable("PlaceRel actual native CanStack recipient absent");
        if(!cache.CanStack(target))return 0x03000004;
        Action.SetSelectedDockAction(DockAction.PlaceHigh);Type=0x19;_savedCarry=carry;
        _subscriptions=M.Docking.RegisterActionHandlers(Action,DockAction.PlaceHigh);
        var marker=pre.Poses[pre.ClosestIndex].Marker;if(marker is null)return 0x03000002;_marker=unchecked((short)marker.Code);
        _prepared?.Prep();_prepared?.WatcherEnding();
        _prepared=new(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
        _prepared.SetDeleteOnCompletion(false);
        _prepared.AddAction(new NativeTurnTowardsObjectRunner(M,Action.ObjectId,_marker,0,true,false){SuppressTrackLocking=true},false,false);
        if(CozmoCubes.IsLightCube(target.Type)&&!_light){M.Robot.Lights.Cubes.PlayNativeInteracting(Action.ObjectId);_light=true;}
        if(_squint!=0){M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);_squint=0;}
        var initialized=Action.RunInitInternal();if(initialized!=ActionResult.Success)return unchecked((int)initialized);
        uint started=_prepared.Update();if(started!=0&&started!=EngineActionResult.Running)return unchecked((int)started);
        owner.BehaviorManagerOwner?.DisableReactionsWithLock("dockActions",Enumerable.Range(0,21).Select(i=>i==8).ToArray(),true);
        return 0;
    }
    private void AddSquint()
    {
        if((int)M.Robot.Engine.Needs.SevereNeeds.SelectedNeed==1)return;
        var face=new ProceduralFacePose();foreach(var eye in new[]{face.Left,face.Right})
        {eye[EyeParam.EyeScaleY]=.35f;eye[EyeParam.EyeScaleX]=1.05f;eye[EyeParam.UpperLidAngle]=-10f;}
        var track=new StreamTrack<FaceFrame>();track.AddKeyFrameToBack(new FaceFrame(0,face));track.AddKeyFrameToBack(new FaceFrame(250,new ProceduralFacePose()));
        _squint=M.Robot.Animations.Scheduler.Layers.Face.AddPersistentLayer("DockSquint",track);
    }
    public override uint CheckIfDone()
    {
        if(Action.ObjectId==uint.MaxValue)return 0x03000004;
        var owner=M.Robot.Engine.Robot!;
        if(!_sent)
        {
            uint result=_prepared!.Update();if(result!=0)return result;
            _prepared.Prep();_prepared.WatcherEnding();_prepared=null;
            if(!M.Docking.StartNativeDock(Action.ObjectId,_marker,DockAction.PlaceHigh,Action.Profile,DockingMethod.Default,false))return 0x03000016;
            _sent=true;_latched=false;
        }
        var dock=owner.DockingOwner!;
        if(dock.Active!=0){if(!_latched){_latched=true;AddSquint();}return EngineActionResult.Running;}
        if(!_latched||M.Robot.Motion.BodyMoving)return EngineActionResult.Running;
        if(M.Robot.Motion.HeadMoving)_deadline=-1;
        float now=M.Robot.Engine.Timer.SecondsF;if(_deadline<0)_deadline=now+0f;
        return now>=_deadline?Verify():EngineActionResult.Running;
    }
    internal uint Verify()
    {
        var owner=M.Robot.Engine.Robot!;
        if(Action.SelectedDockAction is not (DockAction.PlaceHigh or DockAction.PlaceLow))return 0x0300001A;
        if(!owner.DockingOwner!.Success)return 0x04000003;
        if(owner.CarryingOwner!.CarriedObjectId is not null)return 0x03000017;
        if(_verify is null)
        {
            _verify=new NativeTurnTowardsObjectRunner(M,_savedCarry,NativeTurnTowardsObjectRunner.AnyCode,0,true,false){SuppressTrackLocking=true,Watcher=Watcher};
            uint first=_verify.Update();if(first!=0&&first!=EngineActionResult.Running)return first; // retains child; first failure does NOT clear.
        }
        uint result=_verify.Update();if(result==EngineActionResult.Running)return result;
        _verify.Prep();_verify.WatcherEnding();_verify=null;
        if(result!=0){M.World.ClearLocatedObjectByIdInCurrentOrigin(_savedCarry);return result;}
        if(Action.SelectedDockAction==DockAction.PlaceHigh&&!_raised)
        {
            _raised=true;_verify=M.Robot.Motion.CreateNativeLiftChild(LiftPresets.LowDockMm,5f);_verify.SuppressTrackLocking=true;_verify.Watcher=Watcher;
            return EngineActionResult.Running;
        }
        return 0;
    }
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        if(Action.SelectedDockAction is (DockAction.PlaceHigh or DockAction.PlaceLow)&&M.World.GetLocatedObjectByIdHelper(Action.ObjectId) is not null)
        {
            // COMPATIBILITY_POLICY: operator 2026-10-28, zero ONLY unused four native raw words.
            // Meaningful target/count1/false and variant0 remain source-exact; normalized words are NOT EXACT_SOURCE.
            return new(Action.ObjectId,0,0,0,0,1,false);
        }
        SteppedBehavior.ReportMissing("Proposed PlaceRel completion: meaningful selectedDock/located target unavailable; full payload withheld, defined scalar result/callback continues; no guessed unused-word policy outside its normal guard");
        return new(0,0,0,0,0,0,false,false);
    }
    public override void WatcherEnding()
    {
        _verify?.Prep();_verify?.WatcherEnding();_verify=null;M.World.SelectedObjectId=null;
        if(M.Robot.Motion.Path.IsActive)M.Paths.Abort();
        var docking=M.Robot.Engine.Robot?.DockingOwner;
        if(docking is not null){if(docking.Active!=0)M.Docking.Abort();docking.MarkerCode=3;docking.TargetId=null;}
        if(_light)M.Robot.Lights.Cubes.StopNativeInteracting(_originalTarget);
        M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);
        _prepared?.Prep();
        if(State!=EngineActionResult.NotStarted)M.Robot.Engine.Robot?.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        _subscriptions?.Dispose();_subscriptions=null;_prepared?.WatcherEnding();_prepared=null;base.WatcherEnding();
    }
}
