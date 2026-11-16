using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Shared configured InteractWithFaces class for Freeplay and Meet, 0x005C1EE8..0x005C2D24.
internal sealed class NativeInteractWithFaces : SteppedBehavior, IDisposable
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

    internal NativeInteractWithFaces(BehaviorFactoryContext source,JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"InteractWithFaces")
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
        { ReportMissing("InteractWithFaces parameters unavailable: "+e.Message); }
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
        if(!Available) { MarkReadinessUnavailable(); ReportMissing("InteractWithFaces original owner or parameters unavailable"); return false; }
        if(!IsRunnableBase(context) || !HostExecutionVersionCurrent(generation)) return false;
        return IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        try { _target=SelectTarget(); return _target!=0; }
        catch(InvalidOperationException error) { MarkReadinessUnavailable(); ReportMissing("InteractWithFaces target unavailable: "+error.Message); return false; }
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
        if(!Current(generation) || _target==0) { InitFailed=true; ReportMissing("InteractWithFaces selected face or owner unavailable"); return; }
        if(_m.Robot.Animations.Library is not { } library || new[]{AnimationTrigger.InteractWithFacesInitialNamed,AnimationTrigger.InteractWithFacesInitialUnnamed,AnimationTrigger.InteractWithFaceTrackingIdle}
            .Any(trigger=>!library.HasAnimationForTrigger(trigger) || library.GetGroup(library.GetAnimationForTrigger(trigger)) is not { Entries.Count: >0 }))
        { InitFailed=true; ReportMissing("M14-006 InteractWithFaces authored animation catalog unavailable"); return; }
        if(!Current(generation))return;
        if(_owner.GetCannedSpeechOwner() is null)
        { InitFailed=true; ReportMissing("M14-006 InteractWithFaces requires the shared authored robot audio device, banks and archive"); return; }
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
            else ReportMissing("M1-034 InteractWithFaces retry emotion owner unavailable; emotion skipped");
            if(!Current(generation))return;
            int prior=_target; _lastImage=_m.Robot.CameraSettings.LastProcessedImageTimestamp;
            try { _target=SelectTarget(); }
            catch(InvalidOperationException error) { ReportMissing("InteractWithFaces retry target unavailable: "+error.Message); Finish(); return; }
            if(!Current(generation))return;
            if(_target!=prior)Verify(generation); else Finish();
        },()=>Current(generation));
    }
    private void Drive(long generation)
    {
        if(!Current(generation))return;
        Phase=2;
        if (!TryReadDriveDistance(_m, _owner, () => Current(generation), out float distance))
        {
            if (Current(generation)) Finish();
            return;
        }
        Phase=3;
        var action = CreateTrackingAction(_m, _owner, _target, distance,
            _clamp, _minClamp, _maxClamp, useAuthoredAudio: true, () => Current(generation));
        if (action is { } driveTrack)
            StartActing(driveTrack.Group, _ => { if (Current(generation)) Track(generation); }, () => Current(generation));
    }

    private void Track(long generation)
    {
        if(!Current(generation))return;
        Phase=4;
        float duration=(float)_owner.Engine.ContextRandom.RandDblInRange(_minTrack,_maxTrack);
        if(!Current(generation))return;
        _deadline=_owner.Engine.Timer.SecondsF+duration;
        var action = CreateTrackingAction(_m, _owner, _target, null,
            _clamp, _minClamp, _maxClamp, useAuthoredAudio: true, () => Current(generation));
        if (action is { } idleTrack)
            StartActing(idleTrack.Group, _ => { if (Current(generation)) Emotion(generation); }, () => Current(generation));
    }

    // 0x005C2420..24A2 transforms the 40 mm point and checks the same-origin map ray.
    internal static bool TryReadDriveDistance(ManipulationSystem motion, EngineRobot owner, Func<bool> current, out float distance)
    {
        distance = 0;
        bool? blocked = null;
        MemoryMap? capturedMap = null;
        uint origin = owner.CurrentOriginId;
        using (motion.World.EnterPoseMutation())
        {
            if (motion.World.Maps.CurrentOrigin == origin && motion.World.Maps.Current is { } map)
            {
                capturedMap = map;
                var pose = owner.GetCheckedMainPose().Transform;
                var from = pose.Translation;
                var to = pose.Apply(new Vec3(40, 0, 0));
                blocked = map.HasCollisionRayWithTypes(new Vec2(from.X, from.Y), new Vec2(to.X, to.Y));
            }
        }
        if (!current()) return false;
        if (owner.CurrentOriginId != origin || motion.World.Maps.CurrentOrigin != origin ||
            !ReferenceEquals(motion.World.Maps.Current, capturedMap))
        {
            ReportMissing("InteractWithFaces map changed during ray query; drive withheld");
            return false;
        }
        if (blocked is null)
        {
            ReportMissing("M1-034 M11-046 InteractWithFaces map unavailable at 0x005C2482; drive withheld");
            return false;
        }
        distance = blocked.Value ? -15f : 40f;
        return true;
    }

    // 0x005C254E..263A and 0x005C27A8..2882 form the drive/track and track/idle compounds.
    internal static (CompoundActionParallel Group, NativeTrackFaceRunner Tracker)? CreateTrackingAction(
        ManipulationSystem motion, EngineRobot owner, int target, float? driveDistance, bool clamp,
        float minClamp, float maxClamp, bool useAuthoredAudio, Func<bool> current)
    {
        if (!current()) return null;
        var group = new CompoundActionParallel(() => owner.Engine.Timer.SecondsF);
        var drive = driveDistance is float distance ? new DriveStraightAction(motion, owner, distance, 40f, false) : null;
        if (drive is not null && !Add(drive)) return null;
        var tracker = new NativeTrackFaceRunner(motion, owner, target, headOnly: drive is not null,
            BitConverter.UInt32BitsToSingle(0x3D8EFA35), clamp, minClamp, maxClamp, drive?.Tag ?? 0);
        if (!Add(tracker)) return null;
        if (drive is null)
        {
            var idle = new TriggerAnimationAction(motion.Robot, new(AnimationTrigger.InteractWithFaceTrackingIdle, 0, true, 0, 60f, false));
            if (useAuthoredAudio) idle.UseOrdinaryAuthoredAudio(owner);
            if (!Add(idle)) return null;
        }
        return (group, tracker);

        bool Add(ActionRunner child)
        {
            if (group.AddActionGuarded(child, false, current)) return true;
            child.RetireNeverAdmittedTree();
            group.RetireNeverAdmittedTree();
            return false;
        }
    }

    private void Emotion(long generation)
    {
        Phase=5;
        var face=_m.Vision.Faces.GetNativeFace(_owner,_target);
        if(!Current(generation))return;
        if(_owner.MoodOwner is { } mood) mood.Trigger(face?.HasName==true?"InteractWithNamedFace":"InteractWithUnnamedFace",_owner.Engine.Timer.SecondsF);
        else ReportMissing("M1-034 InteractWithFaces terminal emotion owner unavailable; emotion skipped");
    }
    protected override void OnUpdate()
    {
        if (_deadline < 0 || _owner.Engine.Timer.SecondsF < _deadline) return;
        long generation = CaptureHostExecutionVersion();
        _deadline = -1;
        CompleteTrackingDeadline(_owner, Id, () => Current(generation), () => StopActing(true, false), Finish);
    }

    // 0x005C2382..239C publishes objective 0x0C, stops the owned action, then records SeeFace.
    internal static void CompleteTrackingDeadline(EngineRobot owner, string id, Func<bool> current, Action stop, Action finished)
    {
        if (!current()) return;
        owner.Engine.ReportBehaviorObjective((uint)BehaviorObjective.InteractedWithFace, nameof(BehaviorObjective.InteractedWithFace), id);
        if (!current()) return;
        stop();
        if (!current()) return;
        owner.Engine.Needs.RegisterNeedsActionCompleted("SeeFace");
        if (current()) finished();
    }
    protected override void OnStop(BehaviorStopReason reason)
    { Interlocked.Increment(ref _generation); _lastImage=_m.Robot.CameraSettings.LastProcessedImageTimestamp; }
    public void Dispose() { Interlocked.Exchange(ref _retired,1); _changed?.Dispose(); RetireActionCompletionOwner(); }
}
