using Cozmo.Protocol;
using Cozmo.Transport;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

internal sealed record NativeLaserTrackingProfile(float UpdateTimeout, uint PredictionMs,
    float RequiredTrackingTime, float MaxStopDistance, float StopPan, float StopTilt,
    float PanDuration, float TimeToReach);

// TrackGroundPointAction 0x005661AC, for the laser-tag profile of BehaviorTrackLaser.
internal sealed class NativeTrackGroundPointRunner : NativeVisionRunner
{
    private readonly EngineRobot _owner;
    private float _lastUpdate, _savedIdle = -1f;
    private bool _ending, _scheduleInstalled, _freshPoint, _canPredict;
    private NativeLaserObservation _latestPoint = new(0, 0, 0, 0), _previousPoint = new(0, 0, 0, 0);
    private float _withinStopCriteriaSince = -1f;
    private byte _eyeShift;
    private static readonly float PanTolerance = BitConverter.UInt32BitsToSingle(0x3DB2B8C2);
    private static readonly float TiltTolerance = BitConverter.UInt32BitsToSingle(0x3D567750);
    private const float AngleEpsilon = 1e-5f;
    private uint _pendingResult;
    private RetainedSubscription? _subscription;

    internal NativeLaserTrackingProfile Profile { get; }
    protected override string ActionName => "TrackGroundPoint";
    private bool Current => ScopedCurrent && !_ending && !DestructionObserved;
    private DrivingAnimationHandler Driving => M.Robot.Animations.DrivingAnimations;

    internal NativeTrackGroundPointRunner(ManipulationSystem m, EngineRobot owner, NativeLaserTrackingProfile profile, Func<bool> current)
        : base(m, 0x24, 5)
    {
        _owner = owner;
        ScopedOwner = owner;
        ScopedParentCurrent = current;
        Profile = profile;
    }

    public override int Init()
    {
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        Driving.InitOwned(_owner, () => Current, (byte)RequiredTrackMask, Tag, SuppressTrackLocking, true); // 0x00564D54.
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        M.Robot.Animations.WithTrackLayers(_ =>
        {
            var parameters = M.Robot.Animations.Scheduler.LiveIdleParameters;
            _savedIdle = parameters[LiveIdleParam.EyeDartMaxDistance_pix];
            parameters.SetParam(LiveIdleParam.EyeDartMaxDistance_pix, 1f);
        }); // 0x00564DAC..0x00564DC6.
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        _lastUpdate = _owner.Engine.Timer.SecondsF;
        if (!_owner.HasExternalInterface)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Error, null, "TrackGroundPointAction.Init.NoExternalInterface",
                "Robot must have an external interface so action can subscribe to motion observation events.");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return 0x03000000; // 0x00566380..0x005663D2.
        }
        _subscription = _owner.Engine.AnimationMessages.SubscribeMessage(NativeLaserObservation.Tag, true, payload =>
        {
            if (!Current || payload is not NativeLaserObservation point || !(point.Value > 0)) return;
            // 0x00567054..0x005670CE shifts positive observations, then tests the unsigned time gap.
            _previousPoint = _latestPoint;
            _latestPoint = point;
            _freshPoint = true;
            if (Profile.PredictionMs != 0) _canPredict = unchecked(point.Timestamp - _previousPoint.Timestamp) < 250;
        });
        M.Robot.Motion.MoveHeadToAngle(BitConverter.UInt32BitsToSingle(0xBEDF66F3), 1000f, 10000f, 0f, out _, _owner, () => Current);
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        M.Vision.QueueNativeSchedule(new AllVisionModesSchedule(Enumerable.Range(0, AllVisionModesSchedule.ModeCount)
            .Select(i => new VisionModeSchedule(new[] { i == 15 })).ToArray())); // 0x005664CE..0x00566528.
        _scheduleInstalled = true;
        Driving.PlayStartAnimOwned(_owner, () => Current); // 0x00564DF8.
        return Current ? 0 : unchecked((int)EngineActionResult.Cancelled);
    }

    public override uint CheckIfDone()
    {
        if (!Current) return EngineActionResult.Cancelled;
        if (Driving.State == 3) return EngineActionResult.Running;
        if (Driving.State == 4) return _pendingResult; // 0x00564F16..0x00564F34.

        float now = _owner.Engine.Timer.SecondsF;
        var update = UpdateTracking(out float pan, out float tilt, out float distance);
        if (!Current) return EngineActionResult.Cancelled;
        if (update is TrackingUpdate.Observed or TrackingUpdate.Predicted)
            return MoveForUpdate(pan, tilt, distance, now, update == TrackingUpdate.Observed);
        if (update == TrackingUpdate.StopMotors)
        {
            StopHeadTrack();
            if (!Current) return EngineActionResult.Cancelled;
            StopBodyTrack();
            if (!Current) return EngineActionResult.Cancelled;
        }

        if (Profile.UpdateTimeout <= 0f || (_lastUpdate > 0f && now - _lastUpdate <= Profile.UpdateTimeout))
            return EngineActionResult.Running;
        _owner.Engine.EmitSourceLog(EngineLogLevel.Info, "Actions", "ITrackAction.CheckIfDone.Timeout",
            FormattableString.Invariant($"No tracking angle update received in {Profile.UpdateTimeout:F6} seconds, returning done."));
        if (!Current) return EngineActionResult.Cancelled;
        return PlayEnd(Profile.RequiredTrackingTime > AngleEpsilon ? EngineActionResult.Timeout : EngineActionResult.Success);
    }

    private enum TrackingUpdate { None, Observed, Predicted, StopMotors }

    // 0x00566E04..0x00566E72 consumes the fresh flag once and otherwise uses the image clock to gate prediction.
    private TrackingUpdate UpdateTracking(out float pan, out float tilt, out float distance)
    {
        pan = tilt = distance = 0;
        bool observed = _freshPoint;
        _freshPoint = false;
        uint imageAge = unchecked(M.Robot.CameraSettings.LastProcessedImageTimestamp - _latestPoint.Timestamp);
        if (observed)
        {
            if (!(_latestPoint.X < 1000f)) return TrackingUpdate.StopMotors; // +0x13C.
        }
        else if (!_canPredict || imageAge >= Profile.PredictionMs)
            return imageAge != 0 ? TrackingUpdate.StopMotors : TrackingUpdate.None;

        using var historyLock = LogCallbackDeferral.EnterGated(M.World.FrameGate);
        using var poseLock = LogCallbackDeferral.EnterGated(_owner.PoseTreeGate);
        if (!Current) return TrackingUpdate.None;
        var main = _owner.GetCheckedMainPose();
        var previous = Vec3.Zero;
        uint previousTime = 0;
        if (!observed)
        {
            using var last = ReadPose(_previousPoint.Timestamp, true, out previousTime);
            if (last is null || !Current) return TrackingUpdate.None;
            previous = PointRelativeToRobot(last, main, _previousPoint);
        }
        using var historical = ReadPose(_latestPoint.Timestamp, !observed, out uint time);
        if (historical is null || !Current) return TrackingUpdate.None;
        var point = PointRelativeToRobot(historical, main, _latestPoint);
        if (!observed)
        {
            // 0x00566CC2..0x00566D50 uses the two resolved history times. The shipped profile sets +0x146
            // only, so lateral (Y) velocity is extrapolated from the last observation to lastRobotMsgTime.
            float reciprocal = 1f / unchecked(time - previousTime);
            float yVelocity = ((float)point.Y - (float)previous.Y) * reciprocal;
            uint dt = unchecked((_owner.StoredState?.Timestamp ?? 0) - time);
            point = new Vec3(point.X, yVelocity * dt + (float)point.Y, 0);
        }
        float x = (float)point.X, y = (float)point.Y;
        using var target = new Pose3d(Rotation3d.Identity, new Vec3(x, y, 0), parent: historical);
        // 0x00566A7E..0x00566B12 solves the head angle (5 degree tolerance), caps it at -10 degrees,
        // and adds the historical robot yaw to the current-relative bearing.
        if (TurnTowardsPose.ComputeHeadAngleToSeePose(M.Vision.Calibration, target,
                BitConverter.UInt32BitsToSingle(0x3D8EFA35), out tilt, M.Vision.LogLine, main) != 0)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null,
                "TrackGroundPointAction.ComputeAbsAngles.ComputeHeadAngleToSeePoseFailed", "");
            tilt = MathF.Atan2(-49f, x);
        }
        tilt = Radians.Rescale(MathF.Min(tilt, BitConverter.UInt32BitsToSingle(0xBE32B8C2)));
        pan = Radians.Rescale(MathF.Atan2(y, x) + (float)historical.AngleAroundZ);
        distance = x > 50f ? x : 0f;
        return observed ? TrackingUpdate.Observed : TrackingUpdate.Predicted;
    }

    private Pose3d? ReadPose(uint timestamp, bool predicting, out uint resolved)
    {
        resolved = 0;
        uint key = 0;
        HistRobotState? historical = null;
        if (_owner.PoseHistory.ComputeAndInsertNative(timestamp, ref resolved, ref historical, false, ref key, interpolate: false) == 0)
            return new Pose3d(historical!.Pose);

        string step = predicting ? "PredictTrackingHelper" : "UpdateTrackingHelper";
        _owner.Engine.EmitSourceLog(EngineLogLevel.Error, null, $"TrackGroundPointAction.{step}.PoseHistoryError",
            $"Could not get historical pose for point observed at t={timestamp} (lastRobotMsgTime = {_owner.StoredState?.Timestamp ?? 0})");
        if (Current) EngineErrorState.StoreAndMaybeBreak();
        return null;
    }

    private Vec3 PointRelativeToRobot(Pose3d oldRobot, Pose3d currentRobot, NativeLaserObservation observation)
    {
        using var point = new Pose3d(Rotation3d.Identity, new Vec3(observation.X, observation.Y, 0), parent: oldRobot);
        if (!Pose3d.GetWithRespectTo(point, currentRobot, point))
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Error, null,
                "TrackGroundPointAction.GetGroundWrtCurrentRobot.GetWrtFailed", "VERIFY(success): ");
            if (Current) EngineErrorState.StoreAndMaybeBreak();
        }
        return point.Translation; // 0x00566988 also reads the input transform after a failed conversion.
    }

    // ITrackAction::CheckIfDone 0x0056502A..0x005653A4, plus the reachable turn/curvature branches.
    private uint MoveForUpdate(float pan, float tilt, float distance, float now, bool observed)
    {
        float maxTilt = CozmoMotion.MaxHeadAngleRad;
        if (new Radians(tilt) > new Radians(maxTilt)) tilt = maxTilt;
        if (Profile.UpdateTimeout > 0f) _lastUpdate = now;
        float head = M.Robot.Motion.HeadAngleRad;
        float tiltDelta = Radians.Rescale(tilt - head);
        // The shipped profile enables the clamp at 0x005FBF72; its default deadline bounds remain -1.
        if (MathF.Abs(tiltDelta) <= TiltTolerance + AngleEpsilon)
        {
            tiltDelta = MathF.CopySign(TiltTolerance, tiltDelta);
            tilt = Radians.Rescale(head + tiltDelta);
        }
        float eyeY = 0;
        if (MathF.Abs(tiltDelta) >= TiltTolerance - AngleEpsilon)
        {
            uint sent = M.Robot.Motion.MoveHeadToAngle(tilt, MathF.Abs(tiltDelta) / .15f, 10000f, 0f, out _, _owner, () => Current);
            if (!Current) return EngineActionResult.Cancelled;
            if (sent != 0) return PlayEnd(0x03000016);
            if (new Radians(MathF.Abs(tiltDelta)) > new Radians(BitConverter.UInt32BitsToSingle(0x3E32B8C2)))
                eyeY = (MathF.Tan(-tiltDelta) * 17.52f) * 2.4615386f;
        }

        float yaw;
        using (LogCallbackDeferral.EnterGated(M.World.FrameGate))
        using (LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
            yaw = (float)_owner.GetCheckedMainPose().AngleAroundZ;
        if (!Current) return EngineActionResult.Cancelled;

        float panDelta = Radians.Rescale(pan - yaw);
        if (MathF.Abs(panDelta) <= PanTolerance + AngleEpsilon)
        {
            panDelta = MathF.CopySign(PanTolerance, panDelta);
            pan = Radians.Rescale(yaw + panDelta);
        }
        float eyeX = 0;
        if (MathF.Abs(distance) >= AngleEpsilon || MathF.Abs(panDelta) >= PanTolerance - AngleEpsilon)
        {
            if (M.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads)
            {
                _owner.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "ITrackAction.CheckIfDone.OffTreadsStateInvalid",
                    $"[{Tag}] Off tread state {M.Robot.Sensors.OffTreadsState} is invalid for turning in place");
                return Current ? PlayEnd(0x0300000A) : EngineActionResult.Cancelled;
            }
            bool sent = MathF.Abs(distance) < AngleEpsilon
                ? SendTurn(pan, panDelta)
                : SendCurvature(distance, panDelta);
            if (!Current) return EngineActionResult.Cancelled;
            if (!sent) return PlayEnd(0x03000016);
            eyeX = (MathF.Tan(panDelta) * 17.52f) * 2.4615386f;
        }

        if (eyeX != 0f || eyeY != 0f)
            M.Robot.Animations.WithTrackLayers(layers =>
            {
                if (Current)
                    layers.AddEyeShift(ref _eyeShift, "TrackActionEyeShift", Math.Clamp(eyeX, -32f, 32f),
                        Math.Clamp(eyeY, -16f, 16f), 60, 32f, 16f, 1.1f, .8f, .1f);
            });
        if (!Current) return EngineActionResult.Cancelled;
        // 0x0056536C excludes predicted updates from completion. +0x110 is true for laser, so success
        // completes immediately without a driving-end animation when the stop criteria hold.
        return observed && TimeToStop(panDelta, tiltDelta, distance, now)
            ? (Current ? EngineActionResult.Success : EngineActionResult.Cancelled) : EngineActionResult.Running;
    }

    private bool SendTurn(float pan, float panDelta)
    {
        float target;
        using (LogCallbackDeferral.EnterGated(M.World.FrameGate))
        using (LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
        {
            _ = _owner.GetCheckedForwardPose();
            target = Rotation3d.FromAngleAxis(pan, new Vec3(0, 0, 1)).AngleAroundZ;
        }
        return Current && M.Robot.Motion.TurnInPlace(target, MathF.Min(MathF.Abs(panDelta) / Profile.PanDuration,
                BitConverter.UInt32BitsToSingle(0x40A78D36)), BitConverter.UInt32BitsToSingle(0x436DFFDB),
            PanTolerance, 0, true, out _, _owner, () => Current) == 0;
    }

    // 0x00565520..0x005655F0 clips the angle before forming the signed radius, and sends 0x33
    // at 0x005658F8..0x00565904 (reliable, hot=false).
    private bool SendCurvature(float distance, float panDelta)
    {
        short radius = short.MaxValue;
        float magnitude = MathF.Abs(panDelta);
        if (!(magnitude <= PanTolerance + AngleEpsilon))
        {
            float radial = distance / MathF.Cos(MathF.Min(magnitude, BitConverter.UInt32BitsToSingle(0x3FC6D3F2)));
            float squared = radial * radial;
            float length = MathF.Sqrt(squared - distance * distance);
            float signedRadius = MathF.CopySign(squared / (length + length), panDelta);
            radius = unchecked((short)AnimationScheduler.VcvtS32(MathF.Round(signedRadius, MidpointRounding.AwayFromZero)));
        }
        return _owner.SendCheckedOwned(new DriveWheelsCurvature
        {
            WheelSpeedMmps = MathF.Min(distance / Profile.TimeToReach, 220f),
            WheelAccelMmps2 = 10000f,
            Direction = radius
        }, () => Current);
    }

    private bool TimeToStop(float pan, float tilt, float distance, float now)
    {
        if (!(Profile.RequiredTrackingTime > AngleEpsilon)) return false;
        if (!(MathF.Abs(pan) <= Profile.StopPan + AngleEpsilon && MathF.Abs(tilt) <= Profile.StopTilt + AngleEpsilon &&
            distance >= 0f && distance <= Profile.MaxStopDistance))
        {
            _withinStopCriteriaSince = -1f;
            return false;
        }
        if (_withinStopCriteriaSince < 0f)
        {
            _withinStopCriteriaSince = now;
            return false;
        }
        if (!(now - _withinStopCriteriaSince > Profile.RequiredTrackingTime)) return false;
        _owner.Engine.EmitSourceLog(EngineLogLevel.Info, "Actions", "ITrackAction.CheckIfDone.StopCriteriaMet",
            FormattableString.Invariant($"Within tolerances for > {Profile.RequiredTrackingTime:F1}sec (panTol={(Profile.StopPan * 57.295780181884766f):F1}deg tiltTol={(Profile.StopTilt * 57.295780181884766f):F1}deg distTol=[0.0,{Profile.MaxStopDistance:F1}]"));
        return true;
    }

    private uint PlayEnd(uint result)
    {
        Driving.PlayEndAnimOwned(_owner, () => Current);
        _pendingResult = result;
        return Current ? EngineActionResult.Running : EngineActionResult.Cancelled;
    }

    public override bool CanInterrupt() => true;
    public override byte? GetCompletionVariant() => 3;

    public override void WatcherEnding()
    {
        if (_ending) return;
        _ending = true;
        if (_scheduleInstalled && ScopedOwnerCurrent)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Debug, "Actions", "TrackGroundPointAction.Destructor", "Popping vision mode schedule");
            if (ScopedOwnerCurrent) M.Vision.QueueNativeSchedule(null); // 0x005666FC.
        }
        _subscription?.Dispose();
        if (ScopedOwnerCurrent && _eyeShift != 0)
            M.Robot.Animations.WithTrackLayers(layers => layers.RemoveEyeShift(_eyeShift, 0)); // 0x005647BA..0x005647DA.
        if (ScopedOwnerCurrent && _savedIdle >= 0)
            M.Robot.Animations.WithTrackLayers(_ => M.Robot.Animations.Scheduler.LiveIdleParameters.SetParam(
                LiveIdleParam.EyeDartMaxDistance_pix, _savedIdle));
        if (ScopedOwnerCurrent && State != EngineActionResult.NotStarted)
        {
            StopBodyTrack();
            if (ScopedOwnerCurrent) StopHeadTrack();
        }
        if (ScopedOwnerCurrent) Driving.ActionIsBeingDestroyed(_owner, () => ScopedOwnerCurrent); // 0x00564852.
        base.WatcherEnding();
    }
}
