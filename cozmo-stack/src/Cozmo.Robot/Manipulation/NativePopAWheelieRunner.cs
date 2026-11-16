using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-041 - current ordinary Pop Select/Verify/completion; Sparks widening pending.
// POP-A-WHEELIE-1 D3-D5:0055256C..00552A72. Shared IDock owners retained.
// Native Init metadata/shared recipients retain their existing M12 ownership.
internal sealed class NativePopAWheelieRunner:NativeVisionRunner
{
    internal PopAWheelieAction Action {get;}
    private IDisposable? _subscriptions;
    private CompoundActionSequential? _prepared;
    private short _marker=3;
    private Pose3d? _oldPose;
    internal bool CheckPreActionPose {get;set;}=true;
    private bool _sent,_light,_unavailable;
    private bool _ending;
    private ScopedAcquisition? _reactionLease;
    private EngineRobot? Owner => ScopedOwner ?? M.Robot.Engine.Robot;
    private bool Live() => ScopedCurrent && !_ending && !DestructionObserved;
    internal Action<NativeActionUnavailable>? Unavailable;
    protected override string ActionName=>"PopAWheelie";
    public override string? NativeName=>ActionName;
    internal CompoundActionSequential? Prepared=>_prepared;
    internal NativePopAWheelieRunner(ManipulationSystem m,uint target):base(m,0x1D,7)
    {Action=new(m,target);Action.SetSelectedDockAction(DockAction.PopAWheelie);}
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_prepared is not null)_prepared.Watcher=value;}}
    public override uint Update()
    {
        if(_unavailable)return EngineActionResult.Running;
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;Unavailable?.Invoke(error);return EngineActionResult.Running;}
        catch(NativeConfigurationUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;Unavailable?.Invoke(new NativeActionUnavailable(error.Message));return EngineActionResult.Running;}
    }
    // Shared IDock setup: M12-017/M12-034/M12-037; concrete Pop selection D3 below.
    public override int Init()
    {
        if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
        using var mutation=M.World.EnterPoseMutation();var owner=Owner!;
        Action.ResetNativeDockVerification();_sent=false;owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
        var target=M.World.GetLocatedObjectByIdHelper(Action.ObjectId);if(target is null)return 0x03000004;
        var main=owner.GetCheckedMainPose();
        KnownMarker? marker=null;
        if(CheckPreActionPose)
        {
            var pre=DockPreActionPoses.GetNative(new(target,PreActionType.Rolling,true,DockActionBase.PreActionAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>M.NativeObstacles(main,ScopedOwner),M.World);
            if(pre.Result!=ActionResult.Success)return unchecked((int)pre.Result);
            marker=pre.Poses[pre.ClosestIndex].Marker;
        }
        uint selected=Select(target);if(selected!=0)return unchecked((int)selected);
        var subscriptions = ScopedOwner is null ? M.Docking.RegisterActionHandlers(Action,DockAction.PopAWheelie)
            : M.Docking.RegisterActionHandlers(owner, Action, DockAction.PopAWheelie);
        if (!Live() || subscriptions is null)
        {
            subscriptions?.Dispose();
            return unchecked((int)EngineActionResult.Cancelled);
        }
        _subscriptions = subscriptions;
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
        var noObject=new NativeVisuallyVerifyNoObjectRunner(M,raised,new Vec3((float)dimensions.X*.5f,(float)dimensions.Y*.5f,(float)dimensions.Z*.5f)){SuppressTrackLocking=true,ScopedOwner=ScopedOwner,ScopedParentCurrent=ScopedOwner is null ? null : Live};
        noObject.AddIgnoreId(Action.ObjectId);_prepared.AddAction(noObject,false,false);
        _prepared.AddAction(new NativeTurnTowardsObjectRunner(M,Action.ObjectId,_marker,0,true,false){SuppressTrackLocking=true,ScopedOwner=ScopedOwner,ScopedParentCurrent=ScopedOwner is null ? null : Live},false,false);
        if (CozmoCubes.IsLightCube(target.Type) && !_light)
        {
            if (ScopedOwner is null) M.Robot.Lights.Cubes.PlayNativeInteracting(Action.ObjectId);
            else M.Robot.Lights.Cubes.PlayNativeInteracting(Action.ObjectId, Live);
            if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
            _light = true;
        }
        Action.RemoveNativeDockSquintOnInit();

        uint started=_prepared.Update();if(started!=0&&started!=EngineActionResult.Running)return unchecked((int)started);
        if (!Live()) return unchecked((int)EngineActionResult.Cancelled);
        if (ScopedOwner is null) owner.BehaviorManagerOwner?.DisableReactionsWithLock("dockActions",GroundPlacement.ReactionLockTable,true);
        else if (owner.BehaviorManagerOwner is { } manager)
        {
            var reactions = _reactionLease = new ScopedAcquisition();
            if (!manager.DisableScopedReactions("dockActions", GroundPlacement.ReactionLockTable, reactions) || !Live())
                return unchecked((int)EngineActionResult.Cancelled);
            reactions.Completed = true;
        }
        return 0;
    }
    public override uint CheckIfDone()
    {
        if (!Live()) return EngineActionResult.Cancelled;
        if(Action.ObjectId==uint.MaxValue)return 0x03000004;
        var owner=Owner!;
        if(!_sent)
        {
            uint result=_prepared!.Update();if(result!=0)return result;
            var finished = _prepared;
            _prepared = null;
            finished.WatcherEnding(); // 0x005521E4..0x005521F4.
            if (!Live()) return EngineActionResult.Cancelled;
            bool sent = ScopedOwner is null
                ? M.Docking.StartNativeDock(Action.ObjectId,_marker,DockAction.PopAWheelie,Action.Profile,DockingMethod.Default,false)
                : M.Docking.StartNativeDock(owner,Action.ObjectId,_marker,DockAction.PopAWheelie,Action.Profile,DockingMethod.Default,false,Live);
            if (!Live()) return EngineActionResult.Cancelled;
            if (!sent) return 0x03000016;
            _sent=true;
        }
        return Action.UpdateNativeDockVerification(owner, Verify, Live);
    }
    internal uint Select(ObservableObject target)
    {
        var owner=Owner!;
        _oldPose?.Dispose();_oldPose=new Pose3d();
        Pose3d parent=null!;if(!owner.GetCheckedMainPose().TryGetParent(ref parent))return 0x03000004;
        using(parent)if(!Pose3d.GetWithRespectTo(target.NativePose,parent,_oldPose))return 0x03000004;
        Action.SetSelectedDockAction(DockAction.PopAWheelie);
        float dz=(float)_oldPose.Translation.Z-(float)owner.GetCheckedMainPose().Translation.Z;
        if(dz>BitConverter.UInt32BitsToSingle(0x42076666))return 0x03000004;
        if(owner.CarryingOwner!.CarriedObjectId is not null)return 0x03000017;
        return 0;
    }
    // D4: real dock success and Robot::GetPitchAngle; native Radians operator< includes IsNear epsilon.
    internal uint Verify()
    {
        var owner=Owner!;
        if(Action.SelectedDockAction!=DockAction.PopAWheelie)return 0x0300001A;
        if(!owner.DockingOwner!.Success)return 0x04000003;
        float pitch=owner.StoredState?.Pose.Pitch??throw new NativeActionUnavailable("Pop Verify actual Robot pitch input absent");
        return new Radians(pitch)<new Radians(1f)?0x0400000A:0u;
    }
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        SteppedBehavior.ReportMissing("M1-034 M13-041 current ordinary configured Pop completion (Sparks widening pending) POP-A-WHEELIE-1 D5 GetCompletionUnion0055268E..269C/setter0075C460..4A6 reads four undefined unused IDs: skip full public payload; defined target/count1/false and scalar/local callback retained");
        return new(Action.ObjectId,0,0,0,0,1,false,false); // placeholders are unavailable and can never be packed/published.
    }
    public override void WatcherEnding()
    {
        if (_ending || DestructionObserved) return;
        _ending = true;
        _oldPose?.Dispose();
        _oldPose = null;
        var reactions = _reactionLease;
        _reactionLease = null;
        Action.CleanUpNativeDock(Owner, _light, _prepared, State != EngineActionResult.NotStarted, reactions);
        _subscriptions?.Dispose();
        _subscriptions = null;
        _prepared?.WatcherEnding();
        _prepared = null;
        base.WatcherEnding();
    }
}
