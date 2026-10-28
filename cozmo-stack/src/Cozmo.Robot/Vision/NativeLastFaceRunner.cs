using Cozmo.Robot.Behavior;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
namespace Cozmo.Robot.Vision;

//0053B180/18E/338: native ID0/null-Impl semantics, separate from the public -1 adapter.
internal sealed class NativeSmartFaceReference:IDisposable
{
    private sealed class Impl:IDisposable
    {
        internal int Id;
        internal readonly bool External;
        internal readonly CozmoRobot Owner;
        private readonly RetainedSubscription? _deleted,_changed;
        internal Impl(CozmoRobot robot,int id,bool external)
        {
            Id=id;External=external;Owner=robot;
            if(!external)return;
            _deleted=robot.Engine.AnimationMessages.SubscribeMessage(0x4B,true,p=>{if(p is NativeDeletedFace face&&Id==face.Id)Id=0;});
            _changed=robot.Engine.AnimationMessages.SubscribeMessage(0x47,true,p=>{if(p is NativeChangedFace face&&Id==face.OldId)Id=face.NewId;});
        }
        public void Dispose(){_deleted?.Dispose();_changed?.Dispose();}
    }
    private readonly CozmoRobot _robot;
    private readonly EngineRobot? _owner;
    private Impl? _impl;
    internal NativeSmartFaceReference(CozmoRobot robot,int? requested=null)
    {_robot=robot;_owner=robot.Engine.Robot;if(requested is {} id&&_owner?.HasExternalInterface==true)_impl=new(robot,id,true);}
    internal bool HasImpl=>_impl is not null;
    internal int Id=>_impl?.Id??0;
    internal bool IsValid=>_impl is not null&&_impl.Id!=0;
    internal void Reset(){if(_impl is {} value)value.Id=0;}
    internal void UpdateToId(int id){if(_impl is not null){_impl.Id=id;return;}if(_owner?.HasExternalInterface==true)_impl=new(_robot,id,true);}
    internal void CopyFrom(NativeSmartFaceReference source)
    {var next=source._impl is {} value?new Impl(value.Owner,value.Id,value.External):null;var previous=_impl;_impl=next;previous?.Dispose();}
    public void Dispose(){var old=_impl;_impl=null;old?.Dispose();}
}

// fidelity: M13-014, M14-002
//0054B754..C780: finite face0/PI/sayName=false recipient. Inherits the SAME TurnPose core/tag.
internal sealed class NativeLastFaceRunner:NativeTurnTowardsPoseRunner
{
    private readonly NativeSmartFaceReference _requested,_verified;
    private readonly List<RetainedSubscription> _observed=new();
    private ActionRunner? _child;
    private int _faceState;
    private float _best=float.MaxValue;
    private bool _locked;
    private readonly bool _sayName;
    private readonly int _sayTrigger,_noNameTrigger;
    private readonly float _maximum;
    protected override string ActionName=>"TurnTowardsLastFacePose";
    internal NativeLastFaceRunner(Manipulation.ManipulationSystem m):this(m,Radians.Pi,false){}
    internal NativeLastFaceRunner(Manipulation.ManipulationSystem m,float maximum,bool sayName,int sayTrigger=0x23F,int noNameTrigger=0x23F):base(m,Pose3d.Identity,maximum,0x29,0,true)
    {_requested=new(m.Robot,0);_verified=new(m.Robot);_maximum=maximum;_sayName=sayName;_sayTrigger=sayTrigger;_noNameTrigger=noNameTrigger;}
    internal float MaximumTurn=>_maximum;
    internal bool SayName=>_sayName;
    internal int FaceState=>_faceState;
    internal int VerifiedId=>_verified.Id;
    internal int RequestedId=>_requested.Id;
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_child is not null)_child.Watcher=value;}}
    private void SetChild(ActionRunner? next)
    {
        _child?.Prep();var previous=_child;_child=next;previous?.WatcherEnding();
        if(next is not null){next.SuppressTrackLocking=true;next.Watcher=Watcher;}
    }
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        var owner=M.Robot.Engine.Robot??throw new InvalidOperationException("MISSING LastFace actual Robot owner");
        using var cached=new Pose3d("");using var relative=new Pose3d("");
        bool found;
        if(_requested.IsValid)
        {
            var face=M.Vision.Faces.GetFace(_requested.Id);found=face is not null;
            if(face is not null)cached.Assign(face.HeadPose);
        }
        else found=M.Vision.Faces.CopyNativeLastFace(owner.GetNativeOrigin(owner.CurrentOriginId),false,cached);
        if(!found||!cached.TryWithRespectTo(owner.GetCheckedMainPose(),relative))
        {_faceState=3;return 0;}
        SetChild(null);_verified.Reset();_best=float.MaxValue;_faceState=0;AssignGoal(relative);
        if(owner.HasExternalInterface)
        {
            _observed.Add(M.Robot.Engine.AnimationMessages.SubscribeMessage(0x46,true,p=>{if(p is NativeObservedFace face)Observe(face.Id);})); //54BFC4..BFDE appends; dtor owns every handle.
        }
        M.Robot.Motion.LockTracks(5,Tag.ToString(),ActionName);_locked=true;
        return base.Init();
    }
    private void Observe(int id)
    {
        if(_faceState>1)return;
        if(_requested.IsValid){if(_requested.Id==id)_verified.CopyFrom(_requested);return;}
        using var mutation=M.World.EnterPoseMutation();
        if(M.Vision.Faces.GetFace(id) is not {} face||M.Robot.Engine.Robot is not {} owner)return;
        using var relative=new Pose3d("");
        if(!face.HeadPose.TryWithRespectTo(owner.GetCheckedMainPose(),relative))return;
        var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
        float d=x*x;d=d+y*y;d=d+z*z;
        if(!(d<_best))return;
        _best=d;_verified.UpdateToId(id);
    }
    private void Unlock()
    {if(!_locked)return;M.Robot.Motion.UnlockTracks(5,Tag.ToString());_locked=false;}
    private void FineTune()
    {
        using var mutation=M.World.EnterPoseMutation();
        _faceState=2;
        if(_verified.Id==0||M.Vision.Faces.GetFace(_verified.Id) is not {} face){SetChild(null);return;}
        M.Robot.Engine.Needs.RegisterNeedsActionCompleted("SeeFace"); //2E BEFORE conversion.
        using var relative=new Pose3d("");
        if(M.Robot.Engine.Robot is not {} owner||!face.HeadPose.TryWithRespectTo(owner.GetCheckedMainPose(),relative))return;
        if(owner.MoodOwner is {} mood)mood.Trigger("LookAtFaceVerified",M.Robot.Engine.Timer.SecondsF);
        else Behavior.SteppedBehavior.ReportMissing("M1-034 M13-014 LastFace Mood owner absent at0067ADA8; smallest emotion call skipped");
        SetChild(new NativeTurnTowardsPoseRunner(M,relative,MathF.Min(_maximum,BitConverter.UInt32BitsToSingle(0x3F490FDB))));
    }
    private uint Final()
    {if(_verified.Id!=0)M.Vision.Faces.SetTurnedTowardsFace(_verified.Id,true);return 0;}
    public override uint CheckIfDone()
    {
        if(_faceState==0)
        {
            uint result=base.CheckIfDone();if(result==EngineActionResult.Running)return result;
            Unlock();if(result!=0)return result;
            if(_verified.Id!=0)FineTune();
            else {_faceState=1;SetChild(new NativeWaitForImagesRunner(M,10,(VisionMode)2,0));}
            return EngineActionResult.Running;
        }
        if(_faceState==1)
        {
            uint result=_child?.Update()??0;
            if(_verified.Id!=0){FineTune();return EngineActionResult.Running;}
            return result==0?Final():result;
        }
        if(_faceState==2)
        {
            if(_child is null)return Final();
            uint result=_child.Update();if(result!=0)return result;
            if(!_sayName)return Final();
            using var mutation=M.World.EnterPoseMutation();
            var face=M.Vision.Faces.GetFace(_verified.Id);if(face is null)return Final();
            if(face.HasName)
            {
                Behavior.SteppedBehavior.ReportMissing("M13-014 LastFace SayText(name,intent3), animation trigger"+_sayTrigger+" recipient unavailable");
                throw new NativeActionUnavailable("M14-006/M13-014 native LastFace SayText(name,intent3)0054C5AA: intent metadata/trait inputs and actual Robot268 speech provider unavailable");
            }
            if(_noNameTrigger==0x23F)return Final();
            SetChild(new TriggerLiftSafeAnimationAction(M.Robot,new((AnimationTrigger)_noNameTrigger,1,true,0,60f,false)));
            _faceState=3;
        }
        uint terminal=_child?.Update()??0;
        if(terminal!=0)return terminal;
        if(_child is not null)M.Robot.Engine.Needs.RegisterNeedsActionCompleted("SayName"); //retained-child ReInit path54C606..61E.
        return Final();
    }
    public override void Prep(){_child?.Prep();base.Prep();}
    public override void WatcherEnding()
    {SetChild(null);Unlock();foreach(var handle in _observed)handle.Dispose();_observed.Clear();_verified.Dispose();_requested.Dispose();base.WatcherEnding();}
}

// fidelity: M13-027
//0054C7DC..C8CE: eager distinct turns, independent flags, copied Radians and full wrapped proxy.
internal class NativeTurnTowardsFaceWrapperRunner:CompoundActionSequential
{
    internal NativeTurnTowardsFaceWrapperRunner(Manipulation.ManipulationSystem m,ActionRunner wrapped,bool before,bool after,Radians maximum,bool sayName)
        :base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        SeedNativeEmptyCompletion();
        if(before)AddAction(new NativeLastFaceRunner(m,maximum.Value,sayName),false,false);
        AddAction(wrapped,false,false);
        if(after)AddAction(new NativeLastFaceRunner(m,maximum.Value,sayName),false,false);
        SetNativeCompletionProxy(wrapped.Tag);
    }
}
// Existing exact put-down caller convenience; generic construction is shared above.
internal sealed class NativeFaceBeforeAnimationRunner:NativeTurnTowardsFaceWrapperRunner
{
    internal NativeFaceBeforeAnimationRunner(Manipulation.ManipulationSystem m,TriggerAnimationAction animation)
        :base(m,animation,true,false,new Radians(Radians.Pi),false){}
}
