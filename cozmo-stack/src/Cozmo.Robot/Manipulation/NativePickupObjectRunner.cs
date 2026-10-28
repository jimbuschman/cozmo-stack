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
    protected override string ActionName=>"PickupObject";
    internal NativePickupObjectRunner(ManipulationSystem m,uint id):base(m,0x15,7)
    {_target=id;_pickup=new(m,id){CheckPreActionPose=false};}
    internal uint TargetId=>_pickup.ObjectId;
    internal byte LoadState=>_pickup.LiftLoadState;
    internal CompoundActionSequential? Prepared=>_prepared;
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_prepared is not null)_prepared.Watcher=value;if(_verifyTurn is not null)_verifyTurn.Watcher=value;}}
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        var owner=M.Robot.Engine.Robot!;
        _deadline=-1;_sent=false;
        owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        var target=M.World.GetLocatedObjectByIdHelper(_pickup.ObjectId);
        if(target is null||!CubeGeometry.IsCube(target.Type))return 0x03000004;
        var selected=_pickup.SelectNative(target,out var action);if(selected!=ActionResult.Success)return unchecked((int)selected);
        Type=action==DockAction.PickupLow?0x16:0x17;_pickup.SetSelectedDockAction(action);
        _subscriptions=M.Docking.RegisterActionHandlers(_pickup,action);
        var observed=NativeObservedMarkers.Get(target,target.LastObservedTimestamp);
        if(observed.Count==0)return 0x0300001D;
        KnownMarker? marker=observed.Count==1?observed[0]:null;
        if(marker is null)
        {
            float best=float.MaxValue;using var relative=new Pose3d("");
            foreach(var candidate in observed)
            {
                if(!candidate.PoseOnObject.TryWithRespectTo(owner.GetCheckedMainPose(),relative))continue;
                var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
                float d=x*x;d=d+y*y;d=d+z*z;
                if(d<best){best=d;marker=candidate;}
            }
        }
        if(marker is null)return 0x03000002;
        _marker=unchecked((short)marker.Code);
        _prepared?.Prep();_prepared?.WatcherEnding();
        _prepared=new CompoundActionSequential(()=>M.Robot.Engine.Timer.SecondsF){SuppressTrackLocking=true,Watcher=Watcher,SourceLog=SourceLog};
        using var flat=target.NativePose.GetWithRespectToRoot();
        var dimensions=NativeConfigurationGeometry.Dimensions(target,flat);
        var t0=flat.Translation;
        Pose3d root=null!;flat.TryGetParent(ref root);
        using var rootOwner=root;
        using var raised=new Pose3d(flat.Quaternion,new Vec3((float)t0.X,(float)t0.Y,(float)t0.Z+(float)dimensions.Z),parent:root);
        var noObject=new NativeVisuallyVerifyNoObjectRunner(M,raised,new Vec3((float)dimensions.X*.5f,(float)dimensions.Y*.5f,(float)dimensions.Z*.5f)){SuppressTrackLocking=true};
        noObject.AddIgnoreId(_target);_prepared.AddAction(noObject,false,false);_prepared.SetDeleteOnCompletion(false);
        _prepared.AddAction(new NativeTurnTowardsObjectRunner(M,_target,_marker,0,true,false){SuppressTrackLocking=true},false,false);
        if(CozmoCubes.IsLightCube(target.Type)&&!_lightPlayed)
        {M.Robot.Lights.Cubes.PlayNativeInteracting(_target);_lightPlayed=true;}
        if(_squint!=0){M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);_squint=0;}
        uint initial=_prepared.Update();
        if(initial!=0&&initial!=EngineActionResult.Running)return unchecked((int)initial);
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
        if(_pickup.ObjectId==uint.MaxValue)return 0x03000004;
        var owner=M.Robot.Engine.Robot!;
        if(!_sent)
        {
            uint pre=_prepared!.Update();if(pre!=0)return pre;
            _prepared.Prep();_prepared.WatcherEnding();_prepared=null;
            if(!M.Docking.StartNativeDock(_target,_marker,_pickup.SelectedDockAction!.Value,_pickup.Profile))return 0x03000016;
            _sent=true;_latched=false;
        }
        var docking=owner.DockingOwner!;
        if(docking.Active!=0){if(!_latched){_latched=true;AddSquint();}return EngineActionResult.Running;}
        if(!_latched)return EngineActionResult.Running;
        if(M.Robot.Motion.BodyMoving)return EngineActionResult.Running;
        if(M.Robot.Motion.HeadMoving)_deadline=-1f;
        float now=M.Robot.Engine.Timer.SecondsF;if(_deadline<0)_deadline=now+0f;
        if(!(now>=_deadline))return EngineActionResult.Running;
        using var mutation=M.World.EnterPoseMutation();
        var target=M.World.GetLocatedObjectByIdHelper(_pickup.ObjectId);
        var result=_pickup.VerifyPrefix(target,new DockResult(owner.StoredState?.Timestamp??0,docking.Success,0,BlockStatus.NoBlock));
        if(result!=ActionResult.Success)return (uint)result;
        if(_verifyTurn is null)
        {
            _verifyTurn=new(M,_pickup.SavedVerificationPose!,0){SuppressTrackLocking=true,Watcher=Watcher};
        }
        uint turned=_verifyTurn.Update();if(turned==EngineActionResult.Running)return turned;
        return (uint)_pickup.VerifyCarrying(); //ordinary terminal child failures deliberately ignored.
    }
    // 75C460..4A6: native object-interaction setter stores variant0; undefined body publication remains M1-034 guarded.
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        bool flag=_prepared?.Children.Any(c=>c.Type==0x30&&c.State==0x0300001D)==true;
        var carry=M.Robot.Engine.Robot?.CarryingOwner;
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
        _verifyTurn?.Prep();_verifyTurn?.WatcherEnding();_verifyTurn=null;_pickup.Dispose();
        M.World.SelectedObjectId=null;
        if(M.Robot.Motion.Path.IsActive)M.Paths.Abort();
        var docking=M.Robot.Engine.Robot?.DockingOwner;
        if(docking is not null){if(docking.Active!=0)M.Docking.Abort();docking.MarkerCode=3;docking.TargetId=null;}
        if(_lightPlayed)M.Robot.Lights.Cubes.StopNativeInteracting(_target);
        M.Robot.Animations.Scheduler.Layers.Face.RemovePersistentLayer(_squint,250);
        _prepared?.Prep();
        if(State!=EngineActionResult.NotStarted)M.Robot.Engine.Robot?.BehaviorManagerOwner?.RemoveDisableReactionsLock("dockActions");
        _subscriptions?.Dispose();_subscriptions=null;
        _prepared?.WatcherEnding();_prepared=null;
        base.WatcherEnding();
    }
}
