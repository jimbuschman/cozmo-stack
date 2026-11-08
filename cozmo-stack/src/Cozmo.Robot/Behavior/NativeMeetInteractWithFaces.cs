using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured MeetCozmo_InteractWithFaces, 0x005C1EE8..0x005C2D24.
internal sealed class NativeMeetInteractWithFaces : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _owner;
    private readonly float _minTrack, _maxTrack, _minClamp, _maxClamp;
    private readonly bool _clamp, _valid;
    private readonly RetainedSubscription? _changed;
    private int _target, _retired;
    private uint _lastImage;
    private float _deadline=-1;
    private long _generation;
    internal int Phase { get; private set; }
    internal int Target => _target;
    internal float Deadline => _deadline;

    internal NativeMeetInteractWithFaces(BehaviorFactoryContext source,JsonElement config)
        : base("MeetCozmo_InteractWithFaces","InteractWithFaces")
    {
        _m=source.Manipulation!; _owner=source.NativeRobot!;
        try
        {
            var p=config.GetProperty("params");
            _minTrack=p.GetProperty("minTimeToTrackFace_s").GetSingle();
            _maxTrack=p.GetProperty("maxTimeToTrackFace_s").GetSingle();
            _clamp=p.GetProperty("clampSmallAngles").GetBoolean();
            _minClamp=_clamp?p.GetProperty("minClampPeriod_s").GetSingle():-1;
            _maxClamp=_clamp?p.GetProperty("maxClampPeriod_s").GetSingle():-1;
            _valid=float.IsFinite(_minTrack) && float.IsFinite(_maxTrack) && _minTrack<=_maxTrack &&
                float.IsFinite(_minClamp) && float.IsFinite(_maxClamp) && _minClamp<=_maxClamp;
        }
        catch(Exception e) when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { ReportMissing("Meet Interact parameters unavailable: "+e.Message); }
        if(_owner.HasExternalInterface)
            _changed=_owner.Engine.AnimationMessages.SubscribeMessage(0x47,true,p=>
            {
                if(Volatile.Read(ref _retired)==0 && ReferenceEquals(_owner.Engine.Robot,_owner) && p is NativeChangedFace change)
                    Interlocked.CompareExchange(ref _target,change.NewId,change.OldId);
            });
    }

    private bool Available => _valid && NativeScoringAvailable && Volatile.Read(ref _retired)==0 &&
        !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot,_owner) &&
        _owner.NativeBehaviorRuntime is { } runtime && ReferenceEquals(runtime.Manipulation,_m) &&
        runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this);
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_owner.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Volatile.Read(ref _retired)==0 &&
        Interlocked.Read(ref _generation)==version && !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot,_owner);
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==version || Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;

    public override bool IsRunnable(BehaviorContext context)
    {
        using var lease=EnterHostExecutionOwner(context);
        long generation=CaptureHostExecutionVersion();
        if(!Available) { MarkReadinessUnavailable(); ReportMissing("Meet Interact original owner or parameters unavailable"); return false; }
        if(!IsRunnableBase(context) || !HostExecutionVersionCurrent(generation)) return false;
        return IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        try { _target=SelectTarget(); return _target!=0; }
        catch(InvalidOperationException error) { MarkReadinessUnavailable(); ReportMissing("Meet Interact target unavailable: "+error.Message); return false; }
    }
    private int SelectTarget()
    {
        using var mutation=_m.World.EnterPoseMutation();
        var ids=_m.Vision.Faces.GetNativeFaceIds(_owner)
            .Where(id=>_m.Vision.Faces.GetNativeFace(_owner,id) is { } face && face.LastObservedTimestamp>=_lastImage).ToArray();
        if(ids.Length>1 && ids.Any(id=>_m.Vision.Faces.GetNativeFace(_owner,id)?.NativeTrackingTranslation is null))
            throw new InvalidOperationException("native face geometry unavailable for multi-target ranking");
        return BestFaceToTrack.GetNative(ids,_m,_owner,surchargeUnnamed:true);
    }
    private bool Current(long generation)=>HostExecutionVersionCurrent(generation) && Available;
    protected override void OnStart()
    {
        long generation=CaptureHostExecutionVersion();
        _deadline=-1;
        if(!Current(generation) || _target==0) { InitFailed=true; ReportMissing("Meet Interact selected face or owner unavailable"); return; }
        if(_m.Robot.Animations.Library is not { } library || new[]{AnimationTrigger.InteractWithFacesInitialNamed,AnimationTrigger.InteractWithFacesInitialUnnamed,AnimationTrigger.InteractWithFaceTrackingIdle}
            .Any(trigger=>!library.HasAnimationForTrigger(trigger) || library.GetGroup(library.GetAnimationForTrigger(trigger)) is not { Entries.Count: >0 }))
        { InitFailed=true; ReportMissing("M14-006 Meet Interact authored animation catalog unavailable"); return; }
        if(!Current(generation))return;
        if(_owner.GetCannedSpeechOwner() is null)
        { InitFailed=true; ReportMissing("M14-006 Meet Interact requires the shared authored robot audio device, banks and archive"); return; }
        if(Current(generation)) Verify(generation);
    }
    private void Verify(long generation)
    {
        if(!Current(generation))return;
        Phase=1;
        var turn=new NativeLastFaceRunner(_m,_owner,_target,true) { RequireVerifiedFace=true };
        turn.SetNativeNameTriggers((int)AnimationTrigger.InteractWithFacesInitialNamed,(int)AnimationTrigger.InteractWithFacesInitialUnnamed);
        if(!Current(generation)){turn.RetireNeverAdmittedTree();return;}
        var sequence=new CompoundActionSequential(()=>_owner.Engine.Timer.SecondsF);
        if(!sequence.AddActionGuarded(turn,false,()=>Current(generation))) { turn.RetireNeverAdmittedTree(); sequence.RetireNeverAdmittedTree(); return; }
        StartActing(sequence,result=>
        {
            if(!Current(generation))return;
            if(result.Result==0) { Drive(generation); return; }
            if(_owner.MoodOwner is { } mood) mood.Trigger("InteractWithFaceRetry",_owner.Engine.Timer.SecondsF);
            else ReportMissing("M1-034 Meet Interact retry emotion owner unavailable; emotion skipped");
            if(!Current(generation))return;
            int prior=_target; _lastImage=_m.Robot.CameraSettings.LastProcessedImageTimestamp;
            try { _target=SelectTarget(); }
            catch(InvalidOperationException error) { ReportMissing("Meet Interact retry target unavailable: "+error.Message); Finish(); return; }
            if(!Current(generation))return;
            if(_target!=prior)Verify(generation); else Finish();
        },()=>Current(generation));
    }
    private void Drive(long generation)
    {
        if(!Current(generation))return;
        Phase=2;
        bool? blocked=null;
        MemoryMap? capturedMap=null;
        uint capturedOrigin=_owner.CurrentOriginId;
        using(_m.World.EnterPoseMutation())
        {
            if(_m.World.Maps.CurrentOrigin==capturedOrigin && _m.World.Maps.Current is { } map)
            {
                capturedMap=map;
                var transform=_owner.GetCheckedMainPose().Transform;
                var from=transform.Translation;var to=transform.Apply(new Vec3(40,0,0));
                blocked=map.HasCollisionRayWithTypes(new Vec2(from.X,from.Y),new Vec2(to.X,to.Y));
            }
        }
        if(!Current(generation))return;
        if(_owner.CurrentOriginId!=capturedOrigin || _m.World.Maps.CurrentOrigin!=capturedOrigin || !ReferenceEquals(_m.World.Maps.Current,capturedMap))
        { ReportMissing("Meet Interact map changed during ray query; drive withheld"); Finish(); return; }
        if(blocked is null) { ReportMissing("M1-034 M11-046 Meet Interact map unavailable at 0x005C2482; drive withheld"); Finish(); return; }
        Phase=3;
        var parallel=new CompoundActionParallel(()=>_owner.Engine.Timer.SecondsF);
        var drive=new DriveStraightAction(_m,_owner,blocked.Value?-15f:40f,40f,false);
        if(!parallel.AddActionGuarded(drive,false,()=>Current(generation))) { drive.RetireNeverAdmittedTree(); parallel.RetireNeverAdmittedTree(); return; }
        var track=new NativeTrackFaceRunner(_m,_owner,_target,true,BitConverter.UInt32BitsToSingle(0x3D8EFA35),_clamp,_minClamp,_maxClamp,drive.Tag);
        if(!parallel.AddActionGuarded(track,false,()=>Current(generation))) { track.RetireNeverAdmittedTree(); parallel.RetireNeverAdmittedTree(); return; }
        StartActing(parallel,_=>{if(Current(generation))Track(generation);},()=>Current(generation));
    }
    private void Track(long generation)
    {
        if(!Current(generation))return;
        Phase=4;
        float duration=(float)_owner.Engine.ContextRandom.RandDblInRange(_minTrack,_maxTrack);
        if(!Current(generation))return;
        _deadline=_owner.Engine.Timer.SecondsF+duration;
        var parallel=new CompoundActionParallel(()=>_owner.Engine.Timer.SecondsF);
        var track=new NativeTrackFaceRunner(_m,_owner,_target,false,BitConverter.UInt32BitsToSingle(0x3D8EFA35),_clamp,_minClamp,_maxClamp,0);
        if(!parallel.AddActionGuarded(track,false,()=>Current(generation))) { track.RetireNeverAdmittedTree();parallel.RetireNeverAdmittedTree();return; }
        var animation=new TriggerAnimationAction(_m.Robot,new(AnimationTrigger.InteractWithFaceTrackingIdle,0,true,0,60,false));
        if(!Current(generation)){animation.RetireNeverAdmittedTree();parallel.RetireNeverAdmittedTree();return;}
        animation.UseOrdinaryAuthoredAudio(_owner);
        if(!parallel.AddActionGuarded(animation,false,()=>Current(generation))) { animation.RetireNeverAdmittedTree();parallel.RetireNeverAdmittedTree();return; }
        StartActing(parallel,_=>{if(Current(generation))Emotion(generation);},()=>Current(generation));
    }
    private void Emotion(long generation)
    {
        Phase=5;
        var face=_m.Vision.Faces.GetNativeFace(_owner,_target);
        if(!Current(generation))return;
        if(_owner.MoodOwner is { } mood) mood.Trigger(face?.HasName==true?"InteractWithNamedFace":"InteractWithUnnamedFace",_owner.Engine.Timer.SecondsF);
        else ReportMissing("M1-034 Meet Interact terminal emotion owner unavailable; emotion skipped");
    }
    protected override void OnUpdate()
    {
        if(_deadline<0 || _owner.Engine.Timer.SecondsF<_deadline)return;
        long generation=CaptureHostExecutionVersion();
        _owner.Engine.ReportBehaviorObjective(12,"InteractedWithFace",Id);
        if(!Current(generation))return;
        StopActing(true,false);
        if(!Current(generation))return;
        _m.Robot.Engine.Needs.RegisterNeedsActionCompleted("SeeFace");
        if(Current(generation))Finish();
    }
    protected override void OnStop(BehaviorStopReason reason)
    { Interlocked.Increment(ref _generation); _lastImage=_m.Robot.CameraSettings.LastProcessedImageTimestamp; }
    public void Dispose() { Interlocked.Exchange(ref _retired,1); _changed?.Dispose(); RetireActionCompletionOwner(); }
}
