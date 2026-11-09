using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

// fidelity: M14-003
// TrackFaceAction 0x565ADC; the enrollment profile is configured at 0x5FEC84.
internal sealed class NativeTrackFaceRunner : NativeVisionRunner
{
    private readonly EngineRobot _owner;
    private readonly bool _headOnly, _clamp;
    private readonly float _tolerance;
    private readonly float _clampMin, _clampMax;
    private readonly uint _stopOnTag;
    private readonly bool _manualScope;
    private bool _installedTracking;
    private bool _ending;
    private long _endingGeneration;
    private float _nextClamp = -1f;
    private int _faceId;
    private uint _timestamp;
    private float _savedIdle = -1;
    private RetainedSubscription? _changed;
    private readonly object _stopGate = new();
    private bool OwnerCurrent => !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot, _owner);
    private bool Current => OwnerCurrent && !_ending && !DestructionObserved;
    private bool CleanupCurrent => OwnerCurrent && (!_ending || Interlocked.Read(ref _owner.NativeTrackingFaceGeneration)==_endingGeneration);
    protected override string ActionName => "TrackFace";

    internal NativeTrackFaceRunner(ManipulationSystem m, EngineRobot owner, int faceId, bool enrollment = false)
        : base(m, 0x23, enrollment && (m.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads || m.Robot.Sensors.CliffDetectedByEvent) ? 1u : 5u)
    {
        _owner = owner; _faceId = faceId;
        ScopedOwner = owner;
        _headOnly = RequiredTrackMask == 1;
        _clamp = enrollment;
        _tolerance = BitConverter.UInt32BitsToSingle(enrollment ? 0x3D8EFA35u : 0x3D0EFA35u);
        TimeoutSeconds = float.MaxValue;
    }

    // Meet's explicit profile 0x005C2590..25CC; the existing enrollment profile keeps its own defaults.
    internal NativeTrackFaceRunner(ManipulationSystem m, EngineRobot owner, int faceId, bool headOnly,
        float tolerance, bool clamp, float clampMin, float clampMax, uint stopOnTag, bool manualScope = false)
        : base(m, 0x23, headOnly ? 1u : 5u)
    {
        _owner=owner; ScopedOwner=owner; _faceId=faceId; _headOnly=headOnly; _tolerance=tolerance;
        _clamp=clamp; _clampMin=clampMin; _clampMax=clampMax; _stopOnTag=stopOnTag;
        _manualScope=manualScope;
        TimeoutSeconds=float.MaxValue;
    }

    public override int Init()
    {
        if (!Current) return unchecked((int)0x02000000);
        M.Robot.Animations.WithTrackLayers(_ =>
        {
            var p = M.Robot.Animations.Scheduler.LiveIdleParameters;
            _savedIdle = p[LiveIdleParam.EyeDartMaxDistance_pix];
            p.SetParam(LiveIdleParam.EyeDartMaxDistance_pix, 1f);
        });
        if (!Current) return unchecked((int)0x02000000);
        if (_stopOnTag != 0 && !ActionRunnerTagCounter.Global.IsInUse(_stopOnTag))
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"ITrackAction.Init.InvalidOtherActionTag",$"[{Tag}] Waiting on tag {_stopOnTag} to stop this action, but that tag is no longer in use. Stopping now");
            return Current ? unchecked((int)0x03000000) : unchecked((int)EngineActionResult.Cancelled);
        }
        if (!_owner.HasExternalInterface)
        {
            LogCallbackDeferral.RunGated(_stopGate,()=>
            {
                if (!Current) return;
                _owner.Engine.EmitSourceLog(EngineLogLevel.Error,null,"TrackFaceAction.InitInternal.NoExternalInterface","Robot must have an external interface so action can subscribe to face changed ID events.");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            });
            return Current ? unchecked((int)0x03000000) : unchecked((int)EngineActionResult.Cancelled);
        }
        var next = _owner.Engine.AnimationMessages.SubscribeMessage(0x47, true, p =>
        {
            if (Current && p is NativeChangedFace change && change.OldId == _faceId) _faceId = change.NewId;
        });
        if (!Current) { next.Dispose(); return unchecked((int)EngineActionResult.Cancelled); }
        RetainedSubscription.Replace(ref _changed,next);
        if (!Current)
        {
            if (ReferenceEquals(_changed,next)) { _changed=null; next.Dispose(); }
            return unchecked((int)EngineActionResult.Cancelled);
        }
        Interlocked.Increment(ref _owner.NativeTrackingFaceGeneration);
        _owner.NativeTrackingFaceId = _faceId;
        _installedTracking = true;
        _timestamp = 0;
        return 0;
    }

    public override uint CheckIfDone()
    {
        if (!Current) return EngineActionResult.Cancelled;
        if (_stopOnTag != 0 && !ActionRunnerTagCounter.Global.IsInUse(_stopOnTag)) return EngineActionResult.Success;
        float x, y, z, yaw;
        using (LogCallbackDeferral.EnterGated(M.World.FrameGate))
        using (LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
        {
            var face = M.Vision.Faces.GetNativeFace(_owner, _faceId);
            if (face is null || face.LastObservedTimestamp <= _timestamp) return EngineActionResult.Running;
            _timestamp = face.LastObservedTimestamp; // 0x565DC4 before pose conversion.
            if (face.NativeTrackingTranslation is not { } translation)
            {
                Behavior.SteppedBehavior.ReportMissing("M14-001 native camera-frame face position unavailable; tracking command withheld");
                return EngineActionResult.Running;
            }
            using var origin = Pose3d.WrapExisting(face.NativeObservationOrigin!);
            using var pose = new Pose3d(Rotation3d.Identity, translation);
            pose.SetParent(origin);
            using var relative = new Pose3d("");
            var main = _owner.GetCheckedMainPose();
            if (!pose.TryWithRespectTo(main, relative))
            {
                _owner.Engine.EmitSourceLog(EngineLogLevel.Error,null,"TrackFaceAction.UpdateTracking.PoseOriginError","Could not get pose of face w.r.t. robot.");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                return EngineActionResult.Running;
            }
            x = (float)relative.Translation.X; y = (float)relative.Translation.Y; z = (float)relative.Translation.Z;
            yaw = (float)main.AngleAroundZ;
        }
        float square = x * x + y * y;
        if (!(square > 0f) || !Current) return EngineActionResult.Running;
        bool clampNow=false;
        if (_clamp)
        {
            if (_clampMax <= 0) clampNow=true;
            else
            {
                float now=_owner.Engine.Timer.SecondsF;
                if (_nextClamp < 0 || now >= _nextClamp)
                {
                    float delay=(float)_owner.Engine.ContextRandom.RandDblInRange(_clampMin,_clampMax);
                    _nextClamp=now+delay;
                    clampNow=true;
                }
            }
        }
        float tilt = MathF.Atan((z - 49f) / MathF.Sqrt(square));
        float maximumTilt = BitConverter.UInt32BitsToSingle(0x3F46D3F2);
        if (new Radians(tilt) > new Radians(maximumTilt)) tilt = maximumTilt;
        float head = M.Robot.Motion.HeadAngleRad;
        float tiltDelta = Radians.Rescale(tilt - head);
        float pan = Radians.Rescale(MathF.Atan2(y, x) + yaw);
        if (clampNow)
        {
            if (MathF.Abs(tiltDelta) <= _tolerance + Radians.Epsilon) { tiltDelta = MathF.CopySign(_tolerance, tiltDelta); tilt = Radians.Rescale(head + tiltDelta); }
        }
        if (MathF.Abs(tiltDelta) >= _tolerance - Radians.Epsilon)
        {
            uint result=M.Robot.Motion.MoveHeadToAngle(tilt, MathF.Abs(tiltDelta) / 0.15f, 10000f, 0f, out _, _owner,()=>Current);
            if (!Current) return EngineActionResult.Cancelled;
            if (result != 0) return 0x03000016;
        }
        if (!_headOnly)
        {
            float bodyYaw;
            using (LogCallbackDeferral.EnterGated(M.World.FrameGate))
            using (LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
                bodyYaw = (float)_owner.GetCheckedMainPose().AngleAroundZ;
            if (!Current) return EngineActionResult.Cancelled;
            float panDelta = Radians.Rescale(pan - bodyYaw);
            if (clampNow && MathF.Abs(panDelta) <= _tolerance + Radians.Epsilon)
            { panDelta = MathF.CopySign(_tolerance, panDelta); pan = Radians.Rescale(bodyYaw + panDelta); }
            if (MathF.Abs(panDelta) < _tolerance - Radians.Epsilon) return EngineActionResult.Running;
            if (M.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads) return 0x0300000A;
            // 0x5655FC..6B4 sets the copied drive-center rotation, then reads its yaw.
            float bodyTarget;
            using (LogCallbackDeferral.EnterGated(M.World.FrameGate))
            using (LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
            {
                _ = _owner.GetCheckedForwardPose();
                bodyTarget = Rotation3d.FromAngleAxis(pan, new Vec3(0, 0, 1)).AngleAroundZ;
            }
            if (!Current) return EngineActionResult.Cancelled;
            uint result=M.Robot.Motion.TurnInPlace(bodyTarget, MathF.Min(MathF.Abs(panDelta) / 0.4f, BitConverter.UInt32BitsToSingle(0x40A78D36)), BitConverter.UInt32BitsToSingle(0x436DFFDB), _tolerance, 0, true, out _, _owner,()=>Current);
            if (!Current) return EngineActionResult.Cancelled;
            if (result != 0) return 0x03000016;
        }
        return EngineActionResult.Running;
    }

    public override bool CanInterrupt() => true;
    protected override void LockTracks(uint mask,string owner)
    {
        LogCallbackDeferral.RunGated(_stopGate,()=>{ if (Current) M.Robot.Motion.LockTracksOwned((byte)mask,owner,ActionName,_owner); });
    }
    protected override void UnlockTracksInternal(uint mask,string owner)
    {
        LogCallbackDeferral.RunGated(_stopGate,()=>{ if (OwnerCurrent) M.Robot.Motion.UnlockTracksOwned((byte)mask,owner,_owner); });
    }
    protected override void StopHeadTrack() => LogCallbackDeferral.RunGated(_stopGate, () => { if (CleanupCurrent) M.Robot.Motion.StopHeadOwned(_owner,()=>CleanupCurrent); });
    protected override void StopBodyTrack() => LogCallbackDeferral.RunGated(_stopGate, () => { if (CleanupCurrent) M.Robot.Motion.StopBodyOwned(_owner,()=>CleanupCurrent); });
    public override byte? GetCompletionVariant() => 3;
    public override uint GetCompletionUnion() => unchecked((uint)_faceId);
    public override void WatcherEnding()
    {
        if (_ending) return;
        _ending=true;
        _endingGeneration=Interlocked.Read(ref _owner.NativeTrackingFaceGeneration);
        LogCallbackDeferral.RunGated(_stopGate,()=>
        {
            if (!_manualScope || _installedTracking) _owner.NativeTrackingFaceId = 0;
            var old=_changed;_changed=null;old?.Dispose();
            if (CleanupCurrent)
            {
                if (_savedIdle >= 0) M.Robot.Animations.WithTrackLayers(_ => M.Robot.Animations.Scheduler.LiveIdleParameters.SetParam(LiveIdleParam.EyeDartMaxDistance_pix, _savedIdle));
                if (CleanupCurrent && State != EngineActionResult.NotStarted && (!_manualScope || _installedTracking))
                {
                    if (!_headOnly) StopBodyTrack();
                    if (CleanupCurrent) StopHeadTrack();
                }
                if (CleanupCurrent && (!_manualScope || _installedTracking)) M.Robot.Animations.DrivingAnimations.ActionIsBeingDestroyed(_owner, () => CleanupCurrent);
            }
            base.WatcherEnding();
        });
    }
}
