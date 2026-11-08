using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

// TrackFaceAction 0x565ADC; the enrollment profile is configured at 0x5FEC84.
internal sealed class NativeTrackFaceRunner : NativeVisionRunner
{
    private readonly EngineRobot _owner;
    private readonly bool _headOnly, _clamp;
    private readonly float _tolerance;
    private int _faceId;
    private uint _timestamp;
    private float _savedIdle = -1;
    private RetainedSubscription? _changed;
    private readonly object _stopGate = new();
    private bool OwnerCurrent => !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot, _owner);
    private bool Current => OwnerCurrent && !DestructionObserved;
    protected override string ActionName => "TrackFace";

    internal NativeTrackFaceRunner(ManipulationSystem m, EngineRobot owner, int faceId, bool enrollment = false)
        : base(m, 0x23, enrollment && (m.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads || m.Robot.Sensors.CliffDetectedByEvent) ? 1u : 5u)
    {
        _owner = owner; _faceId = faceId;
        _headOnly = RequiredTrackMask == 1;
        _clamp = enrollment;
        _tolerance = BitConverter.UInt32BitsToSingle(enrollment ? 0x3D8EFA35u : 0x3D0EFA35u);
        TimeoutSeconds = float.MaxValue;
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
        if (!_owner.HasExternalInterface) return unchecked((int)0x03000000);
        _changed = _owner.Engine.AnimationMessages.SubscribeMessage(0x47, true, p =>
        {
            if (Current && p is NativeChangedFace change && change.OldId == _faceId) _faceId = change.NewId;
        });
        _owner.NativeTrackingFaceId = _faceId;
        _timestamp = 0;
        return 0;
    }

    public override uint CheckIfDone()
    {
        if (!Current) return EngineActionResult.Cancelled;
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
            if (!pose.TryWithRespectTo(main, relative)) return EngineActionResult.Running;
            x = (float)relative.Translation.X; y = (float)relative.Translation.Y; z = (float)relative.Translation.Z;
            yaw = (float)main.AngleAroundZ;
        }
        float square = x * x + y * y;
        if (!(square > 0f) || !Current) return EngineActionResult.Running;
        float tilt = MathF.Atan((z - 49f) / MathF.Sqrt(square));
        float maximumTilt = BitConverter.UInt32BitsToSingle(0x3F46D3F2);
        if (new Radians(tilt) > new Radians(maximumTilt)) tilt = maximumTilt;
        float head = M.Robot.Motion.HeadAngleRad;
        float tiltDelta = Radians.Rescale(tilt - head);
        float pan = Radians.Rescale(MathF.Atan2(y, x) + yaw);
        if (_clamp)
        {
            if (MathF.Abs(tiltDelta) <= _tolerance + Radians.Epsilon) { tiltDelta = MathF.CopySign(_tolerance, tiltDelta); tilt = Radians.Rescale(head + tiltDelta); }
        }
        if (MathF.Abs(tiltDelta) >= _tolerance - Radians.Epsilon)
        {
            if (M.Robot.Motion.MoveHeadToAngle(tilt, MathF.Abs(tiltDelta) / 0.15f, 10000f, 0f, out _) != 0) return 0x03000016;
            if (!Current) return EngineActionResult.Cancelled;
        }
        if (!_headOnly)
        {
            float bodyYaw;
            using (LogCallbackDeferral.EnterGated(M.World.FrameGate))
            using (LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
                bodyYaw = (float)_owner.GetCheckedMainPose().AngleAroundZ;
            if (!Current) return EngineActionResult.Cancelled;
            float panDelta = Radians.Rescale(pan - bodyYaw);
            if (_clamp && MathF.Abs(panDelta) <= _tolerance + Radians.Epsilon)
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
            if (M.Robot.Motion.TurnInPlace(bodyTarget, MathF.Min(MathF.Abs(panDelta) / 0.4f, BitConverter.UInt32BitsToSingle(0x40A78D36)), BitConverter.UInt32BitsToSingle(0x436DFFDB), _tolerance, 0, true, out _) != 0) return 0x03000016;
        }
        return EngineActionResult.Running;
    }

    public override bool CanInterrupt() => true;
    protected override void StopHeadTrack() => LogCallbackDeferral.RunGated(_stopGate, () => { if (OwnerCurrent) M.Robot.Motion.StopHead(); });
    protected override void StopBodyTrack() => LogCallbackDeferral.RunGated(_stopGate, () => { if (OwnerCurrent) M.Robot.Motion.StopBody(); });
    public override byte? GetCompletionVariant() => 3;
    public override uint GetCompletionUnion() => unchecked((uint)_faceId);
    public override void WatcherEnding()
    {
        _owner.NativeTrackingFaceId = 0;
        _changed?.Dispose(); _changed = null;
        if (Current)
        {
            if (_savedIdle >= 0) M.Robot.Animations.WithTrackLayers(_ => M.Robot.Animations.Scheduler.LiveIdleParameters.SetParam(LiveIdleParam.EyeDartMaxDistance_pix, _savedIdle));
            if (Current && State != EngineActionResult.NotStarted)
            {
                if (!_headOnly) StopBodyTrack();
                if (Current) StopHeadTrack();
            }
            if (Current) M.Robot.Animations.DrivingAnimations.ActionIsBeingDestroyed(_owner, () => Current);
        }
        base.WatcherEnding();
    }
}
