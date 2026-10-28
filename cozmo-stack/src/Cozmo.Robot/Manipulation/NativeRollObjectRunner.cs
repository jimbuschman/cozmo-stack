using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// Proposed Roll own-path R11-R15:00556540..00556DCE, shared IDock bodies retain existing owners.
// Native Init metadata/shared recipients retain their existing M12 ownership.
internal sealed class NativeRollObjectRunner:NativeVisionRunner
{
    private readonly uint _originalTarget;
    internal RollObjectAction Action {get;}
    private IDisposable? _subscriptions;
    private CompoundActionSequential? _prepared;
    private ActionRunner? _verify;
    private short _marker=3;
    private short _topMarker;
    private Pose3d? _oldPose;
    internal bool CheckPreActionPose {get;set;}=true;
    private bool _sent,_latched,_light,_unavailable;
    private float _deadline=-1;
    private byte _squint;
    internal Action<NativeActionUnavailable>? Unavailable;
    protected override string ActionName=>"RollObject";
    internal short SavedTopMarker=>_topMarker;
    internal ActionRunner? VerifyChild=>_verify;
    internal CompoundActionSequential? Prepared=>_prepared;
    internal NativeRollObjectRunner(ManipulationSystem m,uint target):base(m,0x1F,7)
    {_originalTarget=target;Action=new(m,target);Action.SetSelectedDockAction(DockAction.RollLow);}
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
    // Shared IDock setup: M12-017/M12-034/M12-037; concrete Roll selection R12 below.
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();var owner=M.Robot.Engine.Robot!;
        _deadline=-1;_sent=false;owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        var target=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);if(target is null)return 0x03000004;
        var main=owner.GetCheckedMainPose();
        KnownMarker? marker=null;
        if(CheckPreActionPose)
        {
            var pre=DockPreActionPoses.GetNative(new(target,PreActionType.Rolling,true,DockActionBase.PreActionAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>M.NativeObstacles(main),M.World);
            if(pre.Result!=ActionResult.Success)return unchecked((int)pre.Result);
            marker=pre.Poses[pre.ClosestIndex].Marker;
        }
        uint selected=Select(target);if(selected!=0)return unchecked((int)selected);
        _subscriptions=M.Docking.RegisterActionHandlers(Action,Action.DeepRoll?DockAction.DeepRollLow:DockAction.RollLow);
        if(!CheckPreActionPose)
        {
            var observed=NativeObservedMarkers.Get(target,target.LastObservedTimestamp);
            if(observed.Count==0)return 0x0300001D;
            marker=observed.Count==1?observed[0]:null;
            if(marker is null)
            {
                float best=float.MaxValue;using var relative=new Pose3d("");
                foreach(var candidate in observed)
                {
                    if(!candidate.PoseOnObject.TryWithRespectTo(main,relative))continue;
                    var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
                    float d=x*x;d=d+y*y;d=d+z*z;
                    if(d<best){best=d;marker=candidate;}
                }
            }
        }
        if(marker is null)return 0x03000002;_marker=unchecked((short)marker.Code);
        _prepared?.Prep();_prepared?.WatcherEnding();
        _prepared=new(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
        _prepared.SetDeleteOnCompletion(false);
        // Actual IDock C0=true: verify empty space above the target before turning/docking.
        using var flat=target.NativePose.GetWithRespectToRoot();var dimensions=NativeConfigurationGeometry.Dimensions(target,flat);
        var t0=flat.Translation;Pose3d root=null!;flat.TryGetParent(ref root);using var rootOwner=root;
        using var raised=new Pose3d(flat.Quaternion,new Vec3((float)t0.X,(float)t0.Y,(float)t0.Z+(float)dimensions.Z),parent:root);
        var noObject=new NativeVisuallyVerifyNoObjectRunner(M,raised,new Vec3((float)dimensions.X*.5f,(float)dimensions.Y*.5f,(float)dimensions.Z*.5f)){SuppressTrackLocking=true};
        noObject.AddIgnoreId(Action.ObjectId);_prepared.AddAction(noObject,false,false);
        _prepared.AddAction(new NativeTurnTowardsObjectRunner(M,Action.ObjectId,_marker,0,true,false){SuppressTrackLocking=true},false,false);
        if(CozmoCubes.IsLightCube(target.Type)&&!_light){M.Robot.Lights.Cubes.PlayNativeInteracting(Action.ObjectId);_light=true;}
        if(_squint!=0){M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);_squint=0;}
        
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
            if(!M.Docking.StartNativeDock(Action.ObjectId,_marker,Action.DeepRoll?DockAction.DeepRollLow:DockAction.RollLow,Action.Profile,DockingMethod.Default,false))return 0x03000016;
            _sent=true;_latched=false;
        }
        var dock=owner.DockingOwner!;
        if(dock.Active!=0){if(!_latched){_latched=true;AddSquint();}return EngineActionResult.Running;}
        if(!_latched||M.Robot.Motion.BodyMoving)return EngineActionResult.Running;
        if(M.Robot.Motion.HeadMoving)_deadline=-1;
        float now=M.Robot.Engine.Timer.SecondsF;if(_deadline<0)_deadline=now+0f;
        return now>=_deadline?Verify():EngineActionResult.Running;
    }
    internal uint Select(ObservableObject target)
    {
        var owner=M.Robot.Engine.Robot!;
        _oldPose?.Dispose();_oldPose=new Pose3d();
        Pose3d parent=null!;if(!owner.GetCheckedMainPose().TryGetParent(ref parent))return 0x03000004;
        using(parent)if(!Pose3d.GetWithRespectTo(target.NativePose,parent,_oldPose))return 0x03000004;
        if(!CubeGeometry.IsCube(target.Type))return 0x03000004;
        _topMarker=NativeRollGeometry.TopMarker(target);
        float dz=(float)_oldPose.Translation.Z-(float)owner.GetCheckedMainPose().Translation.Z;
        if(dz>BitConverter.UInt32BitsToSingle(0x42076666))return 0x03000004;
        if(owner.CarryingOwner!.CarriedObjectId is not null)return 0x03000017;
        Action.SetSelectedDockAction(Action.DeepRoll?DockAction.DeepRollLow:DockAction.RollLow);return 0;
    }
    internal uint Verify()
    {
        var owner=M.Robot.Engine.Robot!;
        if((int)Action.SelectedDockAction!<5||(int)Action.SelectedDockAction!>7)return 0x0300001A;
        if(!owner.DockingOwner!.Success)return 0x04000003;
        if(owner.CarryingOwner!.CarriedObjectId is not null)return 0x03000017;
        if(_verify is null)
        {
            var sequence=new CompoundActionSequential(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
            sequence.SeedNativeEmptyCompletion();
            sequence.AddAction(M.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0xBE860A92),BitConverter.UInt32BitsToSingle(0x3D0EFA35),15f,20f),false,false);
            sequence.AddAction(new NativeVerifyObjectRunner(M,Action.ObjectId,_topMarker){SuppressTrackLocking=true},false,false);
            _verify=sequence;
            uint first=_verify.Update();if(first!=0&&first!=EngineActionResult.Running)return FinishVerify(first);
        }
        return FinishVerify(_verify.Update());
    }
    private uint FinishVerify(uint result)
    {
        if(result==EngineActionResult.Running)return result;
        _verify!.Prep();_verify.WatcherEnding();_verify=null;
        if(result==0)return 0;
        Action.DeepRoll=true;Action.SetSelectedDockAction(DockAction.DeepRollLow);return 0x0300001D;
    }
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        SteppedBehavior.ReportMissing("M1-034 proposed Roll own-path GetCompletionUnion005567E0..EE/setter0075C400..410 reads four undefined unused IDs: skip full public payload; defined target/count1/false and scalar/local callback retained");
        return new(Action.ObjectId,0,0,0,0,1,false,false); // placeholders are unavailable and can never be packed/published.
    }
    public override void WatcherEnding()
    {
        _verify?.Prep();_verify?.WatcherEnding();_verify=null;_oldPose?.Dispose();_oldPose=null;M.World.SelectedObjectId=null;
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
