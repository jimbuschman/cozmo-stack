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
    internal NativeSmartFaceReference(CozmoRobot robot,int? requested=null,EngineRobot? owner=null)
    {_robot=robot;_owner=owner??robot.Engine.Robot;if(requested is {} id&&_owner?.HasExternalInterface==true)_impl=new(robot,id,true);}
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
    private ActionRunner? _preparingChild;
    private int _faceState;
    private float _best=float.MaxValue;
    private bool _locked;
    private readonly object _ownerStopGate = new();
    private readonly bool _sayName;
    private int _sayTrigger,_noNameTrigger;
    private readonly float _maximum;
    private readonly EngineRobot? _actualOwner;
    private bool _aiPublic;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    private int _imagesToWait = 10;
    internal bool RequireVerifiedFace { get; set; }
    internal Func<int, AnimationTrigger>? NamedTriggerCallback;
    protected override string ActionName=>"TurnTowardsLastFacePose";
    internal NativeLastFaceRunner(Manipulation.ManipulationSystem m):this(m,Radians.Pi,false){}
    internal NativeLastFaceRunner(Manipulation.ManipulationSystem m,float maximum,bool sayName,int sayTrigger=0x23F,int noNameTrigger=0x23F):base(m,Pose3d.Identity,maximum,0x29,0,true)
    {_requested=new(m.Robot,0);_verified=new(m.Robot);_maximum=maximum;_sayName=sayName;_sayTrigger=sayTrigger;_noNameTrigger=noNameTrigger;}
    internal NativeLastFaceRunner(Manipulation.ManipulationSystem m,EngineRobot owner,int rawId,bool sayName=false):base(m,Pose3d.Identity,Radians.Pi,0x29,0,true)
    {_requested=new(m.Robot,rawId,owner);_verified=new(m.Robot,owner:owner);_maximum=Radians.Pi;_sayName=sayName;_sayTrigger=_noNameTrigger=0x23F;if(sayName){_actualOwner=owner;ScopedOwner=owner;}}
    // Enrollment's explicit-ID turn uses its supplied max angle and the same
    // checked native camera/origin owner, without the say-name child (5FE914).
    internal NativeLastFaceRunner(Manipulation.ManipulationSystem m, EngineRobot owner, int rawId, float maximum)
        : base(m, Pose3d.Identity, maximum, 0x29, 0, true)
    {
        _requested = new(m.Robot, rawId, owner);
        _verified = new(m.Robot, owner: owner);
        _maximum = maximum;
        _sayTrigger = _noNameTrigger = 0x23F;
        _actualOwner = owner;
        ScopedOwner = owner;
    }
    internal int ImagesToWait { get => _imagesToWait; set => _imagesToWait = value; }
    internal void SetNativeNameTriggers(int named,int unnamed)
    {
        if(!_sayName)M.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,"Actions","TurnTowardsFaceAction.SetSayNameTriggerWithoutSayingName","Installing name triggers while sayName is false");
        _sayTrigger=named;_noNameTrigger=unnamed;
    }
    internal float MaximumTurn=>_maximum;
    internal bool SayName=>_sayName;
    internal int FaceState=>_faceState;
    internal int VerifiedId=>_verified.Id;
    internal ActionRunner? ChildForTest=>_child;
    internal int RequestedId=>_requested.Id;
    internal void ConfigurePublicRequest()
    {
        _aiPublic=true;
        Parameters.SetMaxPanSpeed(0);Parameters.SetPanAccel(0);Parameters.SetPanTolerance(0);
        Parameters.SetMaxTiltSpeed(0);Parameters.SetTiltAccel(0);Parameters.SetTiltTolerance(0);
    }
    public override byte? GetCompletionVariant()=>_aiPublic ? (byte)6 : base.GetCompletionVariant();
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_child is not null)_child.Watcher=value;}}
    private void SetChild(ActionRunner? next)
    {
        var previous=_child;
        if(previous is not null && !ReferenceEquals(_preparingChild,previous))
        {
            _preparingChild=previous;
            try{previous.Prep();}finally{_preparingChild=null;}
        }
        if(DestructionObserved)
        {
            if(next is not null && !next.RetireNeverAdmittedTree())
                Behavior.SteppedBehavior.ReportMissing("M1-034/M13-014 stale LastFace child retirement unavailable");
            return;
        }
        _child=next;
        previous?.WatcherEnding();
        if(DestructionObserved || !ReferenceEquals(_child,next))
        {
            if(next is not null && !next.DestructionObserved && !next.RetireNeverAdmittedTree())
                Behavior.SteppedBehavior.ReportMissing("M1-034/M13-014 stale LastFace child retirement unavailable");
            return;
        }
        if(next is not null){next.SuppressTrackLocking=true;next.Watcher=Watcher;}
    }
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        var owner=_actualOwner??M.Robot.Engine.Robot??throw new InvalidOperationException("MISSING LastFace actual Robot owner");
        if (!ScopedCurrent) return unchecked((int)EngineActionResult.Cancelled);
        using var cached=new Pose3d("");using var relative=new Pose3d("");
        bool found;
        if(_requested.IsValid)
        {
            var face=_actualOwner is null?M.Vision.Faces.GetFace(_requested.Id):M.Vision.Faces.GetNativeFace(owner,_requested.Id);
            found=face is not null && CopyFacePose(face, cached);
        }
        else found=M.Vision.Faces.CopyNativeLastFace(owner.GetNativeOrigin(owner.CurrentOriginId),false,cached);
        if(!found||!cached.TryWithRespectTo(owner.GetCheckedMainPose(),relative))
        {if(RequireVerifiedFace)return unchecked((int)0x0300000E);_faceState=3;return 0;}
        SetChild(null);_verified.Reset();_best=float.MaxValue;_faceState=0;AssignGoal(relative);
        if(owner.HasExternalInterface)
        {
            _observed.Add(M.Robot.Engine.AnimationMessages.SubscribeMessage(0x46,true,p=>{if(p is NativeObservedFace face)Observe(face.Id);})); //54BFC4..BFDE appends; dtor owns every handle.
        }
        if (_actualOwner is null) M.Robot.Motion.LockTracks(5,Tag.ToString(),ActionName);
        else LogCallbackDeferral.RunGated(_ownerStopGate, () =>
        { if (ScopedCurrent && !DestructionObserved) M.Robot.Motion.LockTracksOwned(5,Tag.ToString(),ActionName,_actualOwner); });
        _locked=true;
        if (DestructionObserved || !ScopedCurrent) { Unlock(); return unchecked((int)EngineActionResult.Cancelled); }
        return base.Init();
    }
    private bool CopyFacePose(FaceEntry face, Pose3d destination)
    {
        if (_actualOwner is null) { destination.Assign(face.LegacyHeadPose); return true; }
        if (face.NativeTrackingTranslation is not { } point || face.NativeObservationOrigin is null)
        {
            Behavior.SteppedBehavior.ReportMissing("M14-001 native camera-frame face position unavailable; LastFace turn withheld");
            return false;
        }
        using var origin = Pose3d.WrapExisting(face.NativeObservationOrigin);
        using var pose = new Pose3d(Rotation3d.Identity, point);
        pose.SetParent(origin);
        destination.Assign(pose);
        return true;
    }
    private void Observe(int id)
    {
        if (!ScopedCurrent || DestructionObserved) return;
        if(_faceState>1)return;
        if(_requested.IsValid){if(_requested.Id==id)_verified.CopyFrom(_requested);return;}
        using var mutation=M.World.EnterPoseMutation();
        var owner=_actualOwner??M.Robot.Engine.Robot;
        if(owner is null||(_actualOwner is not null&&!ScopedCurrent))return;
        var face=_actualOwner is null?M.Vision.Faces.GetFace(id):M.Vision.Faces.GetNativeFace(owner,id);
        if(face is null)return;
        using var source=new Pose3d("");
        using var relative=new Pose3d("");
        if(!CopyFacePose(face,source)||!source.TryWithRespectTo(owner.GetCheckedMainPose(),relative))return;
        var t=relative.Translation;float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;
        float d=x*x;d=d+y*y;d=d+z*z;
        if(!(d<_best))return;
        _best=d;_verified.UpdateToId(id);
    }
    private void Unlock()
    {
        if(!_locked)return;
        _locked=false;
        if (_actualOwner is null) M.Robot.Motion.UnlockTracks(5,Tag.ToString());
        else LogCallbackDeferral.RunGated(_ownerStopGate, () => { if (ScopedCurrent) M.Robot.Motion.UnlockTracksOwned(5,Tag.ToString(),_actualOwner); });
    }
    private void FineTune()
    {
        using var mutation=M.World.EnterPoseMutation();
        _faceState=2;
        var owner=_actualOwner??M.Robot.Engine.Robot;
        var face=owner is null?null:_actualOwner is null?M.Vision.Faces.GetFace(_verified.Id):M.Vision.Faces.GetNativeFace(owner,_verified.Id);
        if(_verified.Id==0||face is null){SetChild(null);return;}
        M.Robot.Engine.Needs.RegisterNeedsActionCompleted("SeeFace"); //2E BEFORE conversion.
        if (DestructionObserved || !ScopedCurrent) return;
        using var relative=new Pose3d("");
        using var source=new Pose3d("");
        if(owner is null||!CopyFacePose(face,source)||!source.TryWithRespectTo(owner.GetCheckedMainPose(),relative))return;
        if(owner.MoodOwner is {} mood)mood.Trigger("LookAtFaceVerified",M.Robot.Engine.Timer.SecondsF);
        else Behavior.SteppedBehavior.ReportMissing("M1-034 M13-014 LastFace Mood owner absent at0067ADA8; smallest emotion call skipped");
        if (DestructionObserved || !ScopedCurrent) return;
        SetChild(new NativeTurnTowardsPoseRunner(M,relative,MathF.Min(_maximum,BitConverter.UInt32BitsToSingle(0x3F490FDB))) { ScopedOwner = _actualOwner });
    }
    private uint Final()
    {
        if(!ScopedCurrent||DestructionObserved)return EngineActionResult.Cancelled;
        if(_verified.Id!=0)M.Vision.Faces.SetTurnedTowardsFace(_verified.Id,true);
        return 0;
    }
    public override uint CheckIfDone()
    {
        if (!ScopedCurrent || DestructionObserved) return EngineActionResult.Cancelled;
        if(_faceState==0)
        {
            uint result=base.CheckIfDone();if(result==EngineActionResult.Running)return result;
            if (DestructionObserved || !ScopedCurrent) return EngineActionResult.Cancelled;
            Unlock();if(result!=0)return result;
            if(_verified.Id!=0)FineTune();
            else {_faceState=1;SetChild(new NativeWaitForImagesRunner(M,_imagesToWait,(VisionMode)2,0) { ScopedOwner = _actualOwner });}
            return EngineActionResult.Running;
        }
        if(_faceState==1)
        {
            uint result=_child?.Update()??0;
            if (DestructionObserved || !ScopedCurrent) return EngineActionResult.Cancelled;
            if(_verified.Id!=0){FineTune();return EngineActionResult.Running;}
            if(result!=EngineActionResult.Running && RequireVerifiedFace)return 0x0300000E;
            return result==0?Final():result;
        }
        if(_faceState==2)
        {
            if(_child is null)return Final();
            uint result=_child.Update();if(result!=0)return result;
            if (DestructionObserved || !ScopedCurrent) return EngineActionResult.Cancelled;
            if(!_sayName)return Final();
            using var mutation=M.World.EnterPoseMutation();
            var owner=_actualOwner??M.Robot.Engine.Robot;
            var face=owner is null?null:_actualOwner is null?M.Vision.Faces.GetFace(_verified.Id):M.Vision.Faces.GetNativeFace(owner,_verified.Id);
            if(face is null)return Final();
            if(face.HasName)
            {
                if (_actualOwner is not null && owner is not null && ScopedCurrent && M.Robot.Engine.Options.ResourcesPath is { } resources)
                {
                    SpeechMetadata.Selection selection;
                    try
                    {
                        var metadata = SpeechMetadata.Load(resources);
                        if (!metadata.TrySelect("Name_Normal", face.Name!, M.Robot.Engine.ContextRandom, out selection))
                        { M.Robot.Engine.EmitStackLog("MISSING M14-006 Name_Normal intent is unavailable"); return 0x03000000; }
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
                    { M.Robot.Engine.EmitStackLog("MISSING M14-006 Name_Normal configuration: " + e.Message); return 0x03000000; }
                    var speech = new SayTextAction(M.Robot, owner, face.Name!, selection.Style, selection.Duration, selection.Pitch);
                    int selected;
                    try { selected = NamedTriggerCallback is null ? _sayTrigger : (int)NamedTriggerCallback(_verified.Id); }
                    catch { speech.RetireNeverAdmittedTree(); throw; }
                    if (DestructionObserved || !ScopedCurrent) { speech.RetireNeverAdmittedTree(); return EngineActionResult.Cancelled; }
                    if (selected != 0x23F)
                    {
                        speech.SetCannedTrigger((AnimationTrigger)selected);
                        SetChild(speech);
                        if (DestructionObserved) return EngineActionResult.Cancelled;
                        _faceState = 3;
                        return EngineActionResult.Running;
                    }
                    speech.RetireNeverAdmittedTree();
                }
                string gap=_sayTrigger==0x23F
                    ? "the no-trigger named SayText action and provider are not joined to this caller"
                    : "fit is false; authored animation events and any speech event require a shared persistent Wwise/robot audio recipient";
                Behavior.SteppedBehavior.ReportMissing("M14-006/M13-014 LastFace named SayText: "+gap);
                if (_actualOwner is not null) return 0x03000000;
                throw new NativeActionUnavailable("M14-006/M13-014 native LastFace SayText(name,intent3) 0x0054C5AA: "+gap);
            }
            if(_noNameTrigger==0x23F)return Final();
            var unnamed = new TriggerLiftSafeAnimationAction(M.Robot,new((AnimationTrigger)_noNameTrigger,1,true,0,60f,false));
            if (_actualOwner is not null) unnamed.SetScopedOwner(_actualOwner);
            if (DestructionObserved || !ScopedCurrent) { unnamed.RetireNeverAdmittedTree(); return EngineActionResult.Cancelled; }
            if (RequireVerifiedFace && _actualOwner is { } ordinaryOwner) unnamed.UseOrdinaryAuthoredAudio(ordinaryOwner);
            SetChild(unnamed);
            if(DestructionObserved)return EngineActionResult.Cancelled;
            _faceState=3;
            return EngineActionResult.Running; // 0x0054C706 -> 0x0054C5D2 -> 0x0054C6B4; update next tick.
        }
        uint terminal=_child?.Update()??0;
        if (!ScopedCurrent || DestructionObserved) return EngineActionResult.Cancelled;
        if(terminal!=0)return terminal;
        if(_child is not null)M.Robot.Engine.Needs.RegisterNeedsActionCompleted("SayName"); //retained-child ReInit path54C606..61E.
        return Final();
    }
    public override void Prep(){_child?.Prep();base.Prep();}
    internal override bool RetireNeverAdmittedTree()
    {
        if(!base.RetireNeverAdmittedTree())return false;
        foreach(var handle in _observed)handle.Dispose();_observed.Clear();_verified.Dispose();_requested.Dispose();
        if(_aiPublic)GoalPose.Dispose();
        return true;
    }
    public override void WatcherEnding()
    {
        SetChild(null);Unlock();foreach(var handle in _observed)handle.Dispose();_observed.Clear();_verified.Dispose();_requested.Dispose();
        base.WatcherEnding();
        if(AiNativeEnded is {} ended && _actualOwner is {} owner)
            ended.TrySetResult((State,owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot,owner)));
    }
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
