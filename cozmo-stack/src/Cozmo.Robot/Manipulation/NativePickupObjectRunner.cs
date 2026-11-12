using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

//00553648/00551528/005521AC: actual engine-ticked Pickup/IDock. No Task adapter.
internal sealed class NativePickupObjectRunner:NativeVisionRunner
{
    private readonly uint _target;
    private readonly PickupObjectAction _pickup;
    private IDisposable? _subscriptions;
    private CompoundActionSequential? _prepared;
    private NativeTurnTowardsPoseRunner? _verifyTurn;
    private short _marker;
    private bool _sent,_latched,_lightPlayed;
    private byte _squint;
    private float _deadline=-1f;
    private readonly EngineRobot? _owner;
    private readonly bool _directMarkerTurn;
    private bool _ending;
    private ScopedAcquisition? _reactionLease;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    private bool OwnerCurrent => _owner is null || !_owner.Lifetime.Destroying && ReferenceEquals(M.Robot.Engine.Robot,_owner);
    private bool Current => OwnerCurrent && (ScopedParentCurrent?.Invoke()??true) && !_ending && !DestructionObserved;
    protected override string ActionName=>"PickupObject";
    internal NativePickupObjectRunner(ManipulationSystem m,uint id,bool checkPreActionPose=false):base(m,0x15,7)
    {_target=id;_pickup=new(m,id){CheckPreActionPose=checkPreActionPose};}
    internal NativePickupObjectRunner(ManipulationSystem m,EngineRobot owner,uint id,bool checkPreActionPose,bool directMarkerTurn)
        :this(m,id,checkPreActionPose)
    { _owner=owner; ScopedOwner=owner; _directMarkerTurn=directMarkerTurn; _pickup.ScopedOwner=owner; _pickup.ScopedActionCurrent=()=>Current; }
    internal bool CheckPreActionPose { get=>_pickup.CheckPreActionPose; set=>_pickup.CheckPreActionPose=value; }
    internal uint TargetId=>_pickup.ObjectId;
    internal byte LoadState=>_pickup.LiftLoadState;
    internal CompoundActionSequential? Prepared=>_prepared;
    internal string? HostUnavailable;
    internal Action<NativeActionUnavailable>? Unavailable;
    public override uint Update()
    {
        int depth=Watcher?.HostUpdateDepth??0;
        try { return base.Update(); }
        catch(NativeActionUnavailable error) when(_owner is not null)
        {
            Watcher?.UnwindUnavailableHostUpdate(depth);
            HostUnavailable=error.Message;
            SteppedBehavior.ReportMissing("M12-017 pickup native input unavailable: "+error.Message);
            if(Unavailable is {} unavailable)unavailable(error);
            else _owner.ActionList.DeferHostCleanup(()=>{if(_owner.ActionList.ContainsRunner(this))_owner.ActionList.Cancel(Tag);});
            return EngineActionResult.Running;
        }
    }
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_prepared is not null)_prepared.Watcher=value;if(_verifyTurn is not null)_verifyTurn.Watcher=value;}}
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        var owner=_owner??M.Robot.Engine.Robot!;
        _deadline=-1;_sent=false;
        owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        if(!Current)return unchecked((int)EngineActionResult.Cancelled);
        var target=M.World.GetLocatedObjectByIdHelper(_pickup.ObjectId);
        if(target is null||!CubeGeometry.IsCube(target.Type))return 0x03000004;
        PreActionPoseOutput? pre=null;
        if(_pickup.CheckPreActionPose)
        {
            var main=owner.GetCheckedMainPose();
            pre=DockPreActionPoses.GetNative(new(target,PreActionType.Docking,true,DockActionBase.PreActionAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>M.NativeObstacles(main),M.World);
            if(!Current)return unchecked((int)EngineActionResult.Cancelled);
            if(pre.Result!=ActionResult.Success)return unchecked((int)pre.Result);
        }
        var selected=_pickup.SelectNative(target,out var action);if(selected!=ActionResult.Success)return unchecked((int)selected);
        if(!Current)return unchecked((int)EngineActionResult.Cancelled);
        Type=action==DockAction.PickupLow?0x16:0x17;_pickup.SetSelectedDockAction(action);
        var subscriptions=_owner is null ? M.Docking.RegisterActionHandlers(_pickup,action) : M.Docking.RegisterActionHandlers(owner,_pickup,action);
        if (!Current || subscriptions is null) { subscriptions?.Dispose();return unchecked((int)EngineActionResult.Cancelled); }
        _subscriptions=subscriptions;
        KnownMarker? marker;
        if(pre is not null)marker=pre.Poses[pre.ClosestIndex].Marker;
        else {uint result=NativeDockMarkerSelection.Observed(target,owner.GetCheckedMainPose(),out marker,EmitSource,M.World.MarkerCodeNameInput);if(result!=0)return unchecked((int)result);}
        if(marker is null)return 0x03000002;        _marker=unchecked((short)marker.Code);
        _prepared?.Prep();_prepared?.WatcherEnding();
        _prepared=new CompoundActionSequential(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
        using var flat=target.NativePose.GetWithRespectToRoot();
        var dimensions=NativeConfigurationGeometry.Dimensions(target,flat);
        var t0=flat.Translation;
        Pose3d root=null!;flat.TryGetParent(ref root);
        using var rootOwner=root;
        using var raised=new Pose3d(flat.Quaternion,new Vec3((float)t0.X,(float)t0.Y,(float)t0.Z+(float)dimensions.Z),parent:root);
        var noObject=new NativeVisuallyVerifyNoObjectRunner(M,raised,new Vec3((float)dimensions.X*.5f,(float)dimensions.Y*.5f,(float)dimensions.Z*.5f)){SuppressTrackLocking=true,ScopedOwner=_owner,ScopedParentCurrent=_owner is null?null:()=>Current};
        noObject.AddIgnoreId(_target);_prepared.AddAction(noObject,false,false);
        if(!Current)return unchecked((int)EngineActionResult.Cancelled);
        _prepared.SetDeleteOnCompletion(false);
        _prepared.AddAction(new NativeTurnTowardsObjectRunner(M,_target,_directMarkerTurn?NativeTurnTowardsObjectRunner.AnyCode:_marker,0,true,false){SuppressTrackLocking=true,ScopedOwner=_owner,ScopedParentCurrent=_owner is null?null:()=>Current},false,false);
        if(!Current)return unchecked((int)EngineActionResult.Cancelled);
        if(CozmoCubes.IsLightCube(target.Type)&&!_lightPlayed)
        {if(_owner is null)M.Robot.Lights.Cubes.PlayNativeInteracting(_target);else M.Robot.Lights.Cubes.PlayNativeInteracting(_target,()=>Current);if(!Current)return unchecked((int)EngineActionResult.Cancelled);_lightPlayed=true;}
        if(_squint!=0){M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);if(!Current)return unchecked((int)EngineActionResult.Cancelled);_squint=0;}
        uint initial=_prepared.Update();
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        if(initial!=0&&initial!=EngineActionResult.Running)return unchecked((int)initial);
        if(_owner is null)owner.BehaviorManagerOwner?.DisableReactionsWithLock("dockActions",Enumerable.Range(0,21).Select(i=>i==8).ToArray(),true);
        else if(owner.BehaviorManagerOwner is {} manager)
        {
            var lease=_reactionLease=new ScopedAcquisition();
            if(!manager.DisableScopedReactions("dockActions",GroundPlacement.ReactionLockTable,lease) || !Current)
                return unchecked((int)EngineActionResult.Cancelled);
            lease.Completed=true;
        }
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
        if (!Current) return EngineActionResult.Cancelled;
        if(_pickup.ObjectId==uint.MaxValue)return 0x03000004;
        var owner=_owner??M.Robot.Engine.Robot!;
        if(!_sent)
        {
            uint pre=_prepared!.Update();if(pre!=0)return pre;
            if (!Current) return EngineActionResult.Cancelled;
            _prepared.Prep();_prepared.WatcherEnding();_prepared=null;
            if (!Current) return EngineActionResult.Cancelled;
            bool sent = _owner is null ? M.Docking.StartNativeDock(_target,_marker,_pickup.SelectedDockAction!.Value,_pickup.Profile)
                : M.Docking.StartNativeDock(owner,_target,_marker,_pickup.SelectedDockAction!.Value,_pickup.Profile,current:()=>Current);
            if (!Current) return EngineActionResult.Cancelled;
            if(!sent)return 0x03000016;
            _sent=true;_latched=false;
        }
        var docking=owner.DockingOwner!;
        if(docking.Active!=0){if(!_latched){_latched=true;AddSquint();if(!Current)return EngineActionResult.Cancelled;}return EngineActionResult.Running;}
        if(!_latched)return EngineActionResult.Running;
        if(M.Robot.Motion.BodyMoving)return EngineActionResult.Running;
        if(M.Robot.Motion.HeadMoving)_deadline=-1f;
        float now=M.Robot.Engine.Timer.SecondsF;if(_deadline<0)_deadline=now+0f;
        if(!(now>=_deadline))return EngineActionResult.Running;
        using var mutation=M.World.EnterPoseMutation();
        var target=M.World.GetLocatedObjectByIdHelper(_pickup.ObjectId);
        var result=_pickup.VerifyPrefix(target,new DockResult(owner.StoredState?.Timestamp??0,docking.Success,0,BlockStatus.NoBlock));
        if(!Current)return EngineActionResult.Cancelled;
        if(result!=ActionResult.Success)return (uint)result;
        if(_verifyTurn is null)
        {
            _verifyTurn=new(M,_pickup.SavedVerificationPose!,0){SuppressTrackLocking=true,Watcher=Watcher,ScopedOwner=_owner,ScopedParentCurrent=_owner is null?null:()=>Current};
        }
        uint turned=_verifyTurn.Update();if (!Current) return EngineActionResult.Cancelled;if(turned==EngineActionResult.Running)return turned;
        var carried=_pickup.VerifyCarrying(); //ordinary terminal child failures deliberately ignored.
        return Current?(uint)carried:EngineActionResult.Cancelled;
    }
    // 75C460..4A6: native object-interaction setter stores variant0; undefined body publication remains M1-034 guarded.
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        bool flag=_prepared?.Children.Any(c=>c.Type==0x30&&c.State==0x0300001D)==true;
        var carry=(_owner??M.Robot.Engine.Robot)?.CarryingOwner;
        if(_pickup.SelectedDockAction is not (DockAction.PickupLow or DockAction.PickupHigh)||carry?.CarriedObjectId is null)
        {
            Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M1-034 Pickup completion5537D4: native non-normal pickup carrier contains undefined words; skip full public payload, retain scalar/local delivery.");
            return new(0,0,0,0,0,0,flag,false);
        }
        var words=new[]{_target,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue};var ids=carry.GetCarryingObjects();
        for(int i=0;i<ids.Length&&i<words.Length;i++)words[i]=ids[i];
        return new(words[0],words[1],words[2],words[3],words[4],unchecked((byte)ids.Length),false);
    }
    public override void WatcherEnding()
    {
        if(_ending || DestructionObserved)return;
        _ending=true;
        long pathGeneration=M.Robot.Motion.Path.ScopeGeneration;
        long dockGeneration=M.Docking.NativeDockGeneration;
        bool SharedCurrent()=>OwnerCurrent && (_owner is null ||
            M.Robot.Motion.Path.ScopeGeneration==pathGeneration && M.Docking.NativeDockGeneration==dockGeneration);
        _verifyTurn?.Prep();_verifyTurn?.WatcherEnding();_verifyTurn=null;_pickup.Dispose();
        if(SharedCurrent())M.World.SelectedObjectId=null;
        if(OwnerCurrent && (_owner is null || M.Robot.Motion.Path.ScopeGeneration==pathGeneration) && M.Robot.Motion.Path.IsActive)
        {
            if(_owner is null)M.Paths.Abort();
            else M.Paths.AbortOwned(_owner,()=>OwnerCurrent && M.Robot.Motion.Path.ScopeGeneration==pathGeneration);
        }
        var docking=(_owner??M.Robot.Engine.Robot)?.DockingOwner;
        if(OwnerCurrent && docking is not null && (_owner is null || M.Docking.NativeDockGeneration==dockGeneration))
        {
            if(docking.Active!=0){if(_owner is null)M.Docking.Abort();else M.Docking.AbortNativeDock(_owner,()=>OwnerCurrent && M.Docking.NativeDockGeneration==dockGeneration);}
            if(OwnerCurrent && (_owner is null || M.Docking.NativeDockGeneration==dockGeneration)){docking.MarkerCode=3;docking.TargetId=null;}
        }
        if(SharedCurrent() && _lightPlayed)
        {if(_owner is null)M.Robot.Lights.Cubes.StopNativeInteracting(_target);else M.Robot.Lights.Cubes.StopNativeInteracting(_target,SharedCurrent);}
        if(SharedCurrent())M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);
        _prepared?.Prep();
        if(_reactionLease is {} lease)
        {
            _reactionLease=null;lease.Cancel();
            if(!SharedCurrent())lease.Completed=false; // retire only this lease after a successor starts.
            _owner?.BehaviorManagerOwner?.RemoveScopedReactions("dockActions",lease);
        }
        else if(SharedCurrent() && State!=EngineActionResult.NotStarted)(_owner??M.Robot.Engine.Robot)?.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        _subscriptions?.Dispose();_subscriptions=null;
        _prepared?.WatcherEnding();_prepared=null;
        base.WatcherEnding();
        if(AiNativeEnded is {} ended && _owner is {} owner)
            ended.TrySetResult((State,owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot,owner)));
    }
}
