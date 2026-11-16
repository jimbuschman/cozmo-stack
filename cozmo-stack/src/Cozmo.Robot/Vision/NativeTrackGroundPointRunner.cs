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
    private bool _ending, _scheduleInstalled, _loggedTrackingGap;
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
            if (_loggedTrackingGap) return;
            _loggedTrackingGap = true;
            SteppedBehavior.ReportMissing("M7-029 TrackGroundPointAction live point tracking at 0x005667D0..0x00566E74 and ITrackAction movement/stop criteria at 0x0056502A..0x00565428 are unavailable");
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

        if (_loggedTrackingGap) return EngineActionResult.Running;

        // The zero-observation route of UpdateTracking 0x00566E40..0x00566E70 and CheckIfDone.
        uint imageTime = M.Robot.CameraSettings.LastProcessedImageTimestamp;
        if (imageTime != 0 && imageTime >= Profile.PredictionMs)
        {
            StopHeadTrack();
            if (!Current) return EngineActionResult.Cancelled;
            StopBodyTrack();
            if (!Current) return EngineActionResult.Cancelled;
        }
        if (Profile.UpdateTimeout <= 0f ||
            (_lastUpdate > 0f && _owner.Engine.Timer.SecondsF - _lastUpdate <= Profile.UpdateTimeout))
            return EngineActionResult.Running;
        _owner.Engine.EmitSourceLog(EngineLogLevel.Info, "Actions", "ITrackAction.CheckIfDone.Timeout",
            FormattableString.Invariant($"No tracking angle update received in {Profile.UpdateTimeout:F6} seconds, returning done."));
        if (!Current) return EngineActionResult.Cancelled;
        _pendingResult = Profile.RequiredTrackingTime > 0 ? EngineActionResult.Timeout : EngineActionResult.Success;
        Driving.PlayEndAnimOwned(_owner, () => Current); // 0x00565498..0x005654C6.
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
