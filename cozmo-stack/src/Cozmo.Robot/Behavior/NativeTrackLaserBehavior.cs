using System.Globalization;
using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// RobotObservedLaserPoint 0x4A, pack 0x007140E2. Points are relative to the observed robot pose.
internal sealed record NativeLaserObservation(uint Timestamp, float Value, short X, short Y)
{
    internal const ushort Tag = 0x4A;
}

internal sealed class NativeTrackLaserBehavior : SteppedBehavior, IDisposable
{
    private enum Phase
    {
        Inactive = 0, BringingHeadDown = 2, Rotate = 3, Exposure = 4, WaitForLaser = 5,
        Respond = 6, Track = 7, Pounce = 8, GetOutBored = 9, WaitForStop = 10, Complete = 11
    }

    private enum Observation { None, Possible, Confirmed }

    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly FeatureGates? _features;
    private readonly RetainedSubscription? _imageSubscription, _laserSubscription;
    private readonly float _attentionDistance, _darkExposure, _darkGain, _imagesToWait, _meanFraction;
    private readonly float _confirmTimeout, _searchAmplitude, _behaviorTimeout, _rotateInterval;
    private readonly float _trackTimeout, _pounceTime, _pounceDistance, _panTolerance, _tiltTolerance;
    private readonly float _backupDistance, _backupDuration, _panMin, _panMax, _durationMin, _durationMax;
    private readonly float _reachMin, _reachMax, _predictionDuration, _objectiveTime;
    private readonly Graph2d _lostTimeoutGraph;
    private static readonly float LowHeadAngle = BitConverter.UInt32BitsToSingle(0xBEDF66F3);
    private Phase _phase;
    private Observation _observation;
    private uint _lastObservation, _exposureTimestamp;
    private float _targetX, _targetY;
    private int _initialMean;
    private float _rotation, _rotateStart, _trackObservedSince, _lostTimeout;
    private bool _everConfirmed, _achieved;
    private (int Exposure, float Gain, bool Color)? _cameraBeforeRun;
    private long _generation;
    private bool _retired;

    private CozmoRobot Facade => _m.Robot;
    private float Now => _robot.Engine.Timer.SecondsF;
    private uint ImageTime => Facade.CameraSettings.LastProcessedImageTimestamp;

    // Factory case 0x0059D026, ctor 0x005FA250, config stores 0x005FA374..0x005FA955.
    // The shipped instance always streamlines and skips the get-out animation (OV62 adopted check).
    internal NativeTrackLaserBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "TrackLaser")
    {
        _m = source.Manipulation!;
        _robot = source.NativeRobot!;
        _features = source.Features;
        float Read(string key) => config.GetProperty(key).GetSingle();
        _ = Read("startIfLaserSeenWithin_sec");  // +0x124, non-streamlined readiness only.
        _attentionDistance = Read("maxDistToGetAttention_mm"); // +0x128.
        _darkExposure = Read("darkenedExposure_ms");
        _darkGain = Read("darkenedGain");
        _imagesToWait = Read("numImagesToWaitForExposureChange");
        _meanFraction = Read("imageMeanFractionForExposureChange");
        _confirmTimeout = Read("maxTimeToConfirm_ms");
        _searchAmplitude = Read("searchAmplitude_deg");
        _behaviorTimeout = Read("maxTimeBehaviorTimeout_sec");
        _rotateInterval = Read("maxTimeBeforeRotate_sec");
        _trackTimeout = Read("trackingTimeout_sec");
        _pounceTime = Read("pounceAfterTrackingFor_sec");
        _pounceDistance = Read("pounceIfWithinDist_mm");
        _panTolerance = Read("pouncePanTol_deg");
        _tiltTolerance = Read("pounceTiltTol_deg");
        _backupDistance = Read("backupDistAfterPounce_mm");
        _backupDuration = Read("backupDurationAfterPounce_sec");
        _panMin = Read("randomInitialSearchPanMin_deg");
        _panMax = Read("randomInitialSearchPanMax_deg");
        _durationMin = Read("minPanDuration_sec");
        _durationMax = Read("maxPanDuration_sec");
        _reachMin = Read("minTimeToReachLaser_sec");
        _reachMax = Read("maxTimeToReachLaser_sec");
        _predictionDuration = Read("predictionDuration_sec");
        _objectiveTime = Read("trackingTimeToAchieveObjective_sec");
        _lostTimeoutGraph = Graph2d.FromJson(config.GetProperty("maxLostLaserTimeoutGraph_sec"))
            ?? throw new NotSupportedException("M7-029 TrackLaser configuration has an empty lost-laser timeout graph");
        if (!config.GetProperty("alwaysStreamline").GetBoolean() || !config.GetProperty("skipGetOutAnim").GetBoolean())
            throw new NotSupportedException("M7-029 TrackLaser configuration differs from the shipped streamlined instance");

        // SubscribeToTags {0x4A, 0x43}, table 0x00C715D0 at 0x005FA2AE.
        if (_robot.HasExternalInterface)
        {
            _laserSubscription = _robot.Engine.AnimationMessages.SubscribeMessage(NativeLaserObservation.Tag, true,
                payload => { if (payload is NativeLaserObservation observation) OnLaser(observation); });
            _imageSubscription = _robot.Engine.AnimationMessages.SubscribeMessage(ProcessedImageReport.UnionTag, true,
                payload => { if (payload is ProcessedImageReport report) OnImage(report); });
        }
    }

    private bool OwnerCurrent => !_retired && !_robot.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _robot);
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => ++_generation;
    protected override long CaptureHostExecutionVersion() => _generation;
    protected override bool HostExecutionVersionCurrent(long version) => OwnerCurrent && _generation == version;
    protected override bool HostStopContinuationCurrent(long version) => OwnerCurrent && _generation == version + 1;
    protected override bool HostResumeContinuationCurrent(long version) =>
        OwnerCurrent && (_generation == version || _generation == version + 1);
    protected override bool KeepsRunningWithoutAction => true;
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        // 0x005FAAB0..0x005FAAEC, feature 9, Docking+4 and Carrying+8, then alwaysStreamline.
        if (_features is null || _robot.DockingOwner is null || _robot.CarryingOwner is null)
        {
            MarkReadinessUnavailable();
            ReportMissing("M7-029 TrackLaser runnable inputs unavailable at 0x005FAAB0");
            return false;
        }
        return _features.IsEnabled("Laser") && _robot.DockingOwner.Active == 0 && _robot.CarryingOwner.CarriedObjectId is null;
    }

    protected override void OnStart()
    {
        long version = _generation;
        if (!InitCamera(version))
        {
            if (HostExecutionVersionCurrent(version)) { InitFailed = true; Finish(); }
            return;
        }
        _rotateStart = Now; // 0x005FAC34.
        WaitForExposure();
    }

    private bool InitCamera(long version)
    {
        if (!HostExecutionVersionCurrent(version)) return false;
        _achieved = false;
        _everConfirmed = false;
        var camera = Facade.CameraSettings;
        var previous = camera.QualityState;
        _cameraBeforeRun = (previous.Exposure, previous.Gain, camera.ColorImagesEnabled);

        // 0x005FAC84..0x005FAD0C: false schedules, except one true element for 15 and 8.
        var schedule = new AllVisionModesSchedule(Enumerable.Range(0, AllVisionModesSchedule.ModeCount)
            .Select(i => new VisionModeSchedule(new[] { i is 15 or 8 })).ToArray());
        _m.Vision.QueueNativeSchedule(schedule);
        camera.SetAndDisableAutoExposure(unchecked((int)Unsigned(_darkExposure)), _darkGain);
        if (!HostExecutionVersionCurrent(version)) return false;
        camera.EnableColorImages(true);
        if (!HostExecutionVersionCurrent(version)) return false;
        _initialMean = -1;
        _exposureTimestamp = 0;
        _lostTimeout = (float)_lostTimeoutGraph.EvaluateY(0f);
        return true;
    }

    // 0x005FBBAC: resume clears observation selection, reacquires the camera settings,
    // and starts with the negative backup and lowered head.
    protected override int ResumeInternal()
    {
        HostRunStarting();
        long version = _generation;
        BeginRun(Context, Scope);
        _observation = Observation.None;
        if (!InitCamera(version)) return 1;
        BringHeadDown();
        return HostExecutionVersionCurrent(version) ? 0 : 1;
    }

    private bool State(Phase next, string name)
    {
        long version = _generation;
        if (_phase == next) return HostExecutionVersionCurrent(version);
        _phase = next;
        return SetStateName(name, () => HostExecutionVersionCurrent(version)) && HostExecutionVersionCurrent(version);
    }

    private void Run(ActionRunner action, Action? done = null)
    {
        long version = _generation;
        StartActing(action, done is null ? null : _ => done(), () => HostExecutionVersionCurrent(version));
    }

    private void WaitForExposure()
    {
        if (!State(Phase.Exposure, "WaitingForExposureChange")) return;
        long version = _generation;
        var sequence = new CompoundActionParallel(() => Now);
        sequence.SeedNativeEmptyCompletion();
        int frames = (int)_imagesToWait;
        sequence.AddAction(new NativeWaitForImagesRunner(_m, frames, VisionMode.Count, 0)
        {
            ScopedOwner = _robot,
            ScopedParentCurrent = () => HostExecutionVersionCurrent(version)
        }, false, false);
        sequence.AddAction(new TriggerAnimationAction(Facade, new((AnimationTrigger)0x223, 1, true, 0, 60f, false)), false, false);
        Run(sequence, () =>
        {
            _exposureTimestamp = ImageTime;
            WaitForLaser();
            Debug("TransitionToWaitForExposureChange.AssumingChanged",
                $"{frames} images have passed. Assuming exposure change has taken effect at t={_exposureTimestamp}ms");
        }); // 0x005FADC8 and callback 0x005FC68C.
    }

    private void WaitForLaser() => State(Phase.WaitForLaser, "WaitingForLaser"); // 0x005FB380.

    protected override void OnUpdate()
    {
        long version = _generation;
        if (_phase == Phase.Exposure)
        {
            if (_exposureTimestamp == 0) return;
            StopActing(keepAction: false, viaCallback: false);
            if (!HostExecutionVersionCurrent(version)) return;
            WaitForLaser(); // 0x005FB220 falls into state 5 on the same tick.
            if (!HostExecutionVersionCurrent(version)) return;
        }
        switch (_phase)
        {
            case Phase.WaitForLaser:
                if (_observation == Observation.Confirmed)
                {
                    Event("confirmed_laser");
                    if (HostExecutionVersionCurrent(version)) Respond();
                }
                else if (TimedOut())
                {
                    if (HostExecutionVersionCurrent(version)) ExitAfterTimeout();
                }
                else if (_rotateStart + _rotateInterval < Now)
                    Rotate();
                return;
            case Phase.Track:
                if (_observation != Observation.None)
                {
                    if (_trackObservedSince == -1f) _trackObservedSince = Now;
                    if (Now - _trackObservedSince > _objectiveTime) _achieved = true;
                }
                return;
            case Phase.Pounce:
            case Phase.GetOutBored:
                return;
            case Phase.WaitForStop:
            case Phase.Complete:
                Finish();
                return;
            default:
                if (TimedOut() && HostExecutionVersionCurrent(version)) ExitAfterTimeout();
                return;
        }
    }

    private bool TimedOut()
    {
        uint image = ImageTime;
        if (_everConfirmed && (_lostTimeout * 1000f) + (float)_lastObservation < image)
        {
            Info("CheckForTimeout.NoLaserTimeout", FormattableString.Invariant($"No laser found, giving up after {_lostTimeout:F6}s"));
            Event("no_laser_timeout", _lostTimeout * 1000f, "0");
            return true;
        }
        if (!_everConfirmed && _phase == Phase.WaitForLaser && (float)_exposureTimestamp + _confirmTimeout < image)
        {
            Debug("CheckForTimeout.NeverConfirmed", $"LastImg:{image}ms LastObs:{_lastObservation}ms MaxTime:{Unsigned(_confirmTimeout)}ms");
            Event("laser_never_confirmed");
            return true;
        }
        if (!((float)RunningPenaltyClockSec + _behaviorTimeout < Now)) return false;
        Info("CheckForTimeout.BehaviorTimeout",
            FormattableString.Invariant($"Started:{RunningPenaltyClockSec:F1}, Now:{Now:F1}, Timeout:{_behaviorTimeout:F1}"));
        Event("ran_until_max_timeout", _behaviorTimeout);
        return true;
    }

    private void ExitAfterTimeout()
    {
        if (!_everConfirmed) { Finish(); return; }
        if (!State(Phase.GetOutBored, "GetOutBored")) return;
        long version = _generation;
        StopActing(keepAction: false, viaCallback: false);
        if (HostExecutionVersionCurrent(version)) State(Phase.WaitForStop, "WaitForStop"); // 0x005FBA50, skipGetOutAnim.
    }

    private void Rotate()
    {
        if (!State(Phase.Rotate, "RotateToWatchingNewArea")) return;
        long version = _generation;
        _rotateStart = Now;
        var random = _robot.Engine.ContextRandom;
        const double degreesToRadians = 0.017453292519943295;
        float angle = new Radians((float)(random.RandDblInRange(_panMin, _panMax) * degreesToRadians)).Value;
        if (!HostExecutionVersionCurrent(version)) return;
        float degrees = _rotation * BitConverter.UInt32BitsToSingle(0x42652EE1);
        double threshold = (double)(_searchAmplitude / (_searchAmplitude - degrees)) * 0.5;
        double direction = random.RandDbl(1.0);
        if (!HostExecutionVersionCurrent(version)) return;
        if (direction < threshold) angle = -angle;
        _rotation = new Radians(_rotation + angle).Value; // +0x11C carries between activations.
        var turn = new NativePanTiltRunner(_m, new PanAndTiltAction
        {
            PanAngleRad = angle, HeadAngleRad = LowHeadAngle, PanIsAbsolute = false, HeadIsAbsolute = true
        }) { ScopedOwner = _robot, ScopedParentCurrent = () => HostExecutionVersionCurrent(version) };
        _observation = Observation.None;
        Run(turn, WaitForLaser); // 0x005FB8D0.
    }

    private void Respond()
    {
        if (!State(Phase.Respond, "RespondToLaser")) return;
        long version = _generation;
        var sequence = new CompoundActionSequential(() => Now);
        sequence.SeedNativeEmptyCompletion();
        using var mutation = _m.World.EnterPoseMutation();
        using var point = new Pose3d(Rotation3d.Identity, new Vec3(_targetX, _targetY, 0), parent: _robot.GetCheckedMainPose());
        sequence.AddAction(new NativeTurnTowardsPoseRunner(_m, point, Radians.Pi)
        {
            ScopedOwner = _robot,
            ScopedParentCurrent = () => HostExecutionVersionCurrent(version)
        }, false, false);
        sequence.AddAction(new TriggerAnimationAction(Facade, new((AnimationTrigger)0x100, 1, true, 1, 60f, false)), false, false);
        Run(sequence, Track); // 0x005FB3E8, 0x005FB4DA, 0x005FB504.
    }

    private void Track()
    {
        if (!State(Phase.Track, "TrackLaser")) return;
        long version = _generation;
        _trackObservedSince = -1f;
        _observation = Observation.None;
        var random = _robot.Engine.ContextRandom;
        float duration = (float)random.RandDblInRange(_durationMin, _durationMax);
        if (!HostExecutionVersionCurrent(version)) return;
        float timeToTarget = (float)random.RandDblInRange(_reachMin, _reachMax);
        if (!HostExecutionVersionCurrent(version)) return;
        float degreesToRadians = BitConverter.UInt32BitsToSingle(0x3C8EFA35);
        var profile = new NativeLaserTrackingProfile(_trackTimeout, Unsigned(_predictionDuration * 1000f),
            _pounceTime, _pounceDistance, _panTolerance * degreesToRadians, _tiltTolerance * degreesToRadians,
            duration, timeToTarget);
        var action = new NativeTrackGroundPointRunner(_m, _robot, profile, () => HostExecutionVersionCurrent(version));
        float start = Now;
        StartActing(action, report =>
        {
            float elapsed = Now - start;
            bool pounce = report.Result == 0;
            _lostTimeout = (float)_lostTimeoutGraph.EvaluateY(Now - (float)RunningPenaltyClockSec);
            Debug("TransitionToTrackLaser.TrackingFinished",
                FormattableString.Invariant($"Result: {report.Result} Time: {elapsed:F6}sec WillPounce: {(pounce ? "Y" : "N")}"));
            Event("tracking_completed", elapsed, pounce ? "1" : "0");
            if (!HostExecutionVersionCurrent(version)) return;
            if (pounce) Pounce(); else Rotate();
        }, () => HostExecutionVersionCurrent(version)); // 0x005FBE84, callback 0x005FC79C.
    }

    private void Pounce()
    {
        if (!State(Phase.Pounce, "Pouncing")) return;
        long version = _generation;
        Run(new TriggerLiftSafeAnimationAction(Facade, new((AnimationTrigger)0x106, 1, true, 0, 60f, false)), () =>
        {
            _robot.Engine.ReportBehaviorObjective(14, "LaserPounced", Id);
            if (HostExecutionVersionCurrent(version)) BringHeadDown();
        }); // 0x005FC0D4, 0x005FC9C6.
    }

    private void BringHeadDown()
    {
        if (!State(Phase.BringingHeadDown, "BringingHeadDown")) return;
        long version = _generation;
        bool Current() => HostExecutionVersionCurrent(version);
        var parallel = new CompoundActionParallel(() => Now);
        parallel.SeedNativeEmptyCompletion();
        parallel.AddAction(Facade.Motion.CreateNativeHeadChild(LowHeadAngle, BitConverter.UInt32BitsToSingle(0x3D0EFA35),
            15f, 20f, _robot, Current), false, false);
        parallel.AddAction(new DriveStraightAction(_m, _robot, _backupDistance, MathF.Abs(_backupDistance) / _backupDuration, true),
            false, false);
        Run(parallel, WaitForLaser); // 0x005FBBCC, 0x005FBC50, 0x005FBC7A.
    }

    private void OnImage(ProcessedImageReport report)
    {
        using var lease = EnterHostExecutionOwner(null);
        if (!OwnerCurrent || _phase != Phase.Exposure || _exposureTimestamp != 0 || !report.Modes.Contains(8u)) return;
        long version = _generation;
        Debug("AlwaysHandle.ImageMean", $"Waiting for exposure change. Current mean: {report.ImageMean}");
        if (!HostExecutionVersionCurrent(version)) return;
        if (_initialMean < 0) { _initialMean = report.ImageMean; return; }
        if (report.ImageMean >= Unsigned(_initialMean * _meanFraction)) return;
        Debug("AlwaysHandle.ObservedExposureChange",
            $"Image mean dropped from {_initialMean} to {report.ImageMean}. Assuming exposure changed at t={report.Timestamp}ms.");
        if (!HostExecutionVersionCurrent(version)) return;
        _exposureTimestamp = report.Timestamp;
        if (_lastObservation == report.Timestamp)
        {
            _everConfirmed = true;
            _observation = Observation.Confirmed;
        }
    }

    private void OnLaser(NativeLaserObservation point)
    {
        using var lease = EnterHostExecutionOwner(null);
        if (!OwnerCurrent || !(point.Value > 0f) || (!EngineRunning && !(point.X < _attentionDistance))) return;
        long version = _generation;
        bool confirmed = _exposureTimestamp != 0;
        Debug("SetLastLaserObservation.SawLaser", $"{(confirmed ? "Confirmed" : "Saw possible")} laser at ({point.X},{point.Y}), t={point.Timestamp}");
        if (!HostExecutionVersionCurrent(version)) return;
        _observation = confirmed ? Observation.Confirmed : Observation.Possible;
        _everConfirmed |= confirmed;
        _lastObservation = point.Timestamp;
        _targetX = point.X;
        _targetY = point.Y; // 0x005FC3F0..0x005FC4DC.
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        long version = ++_generation;
        if (_achieved)
        {
            _robot.Engine.ReportBehaviorObjective(15, "LaserTracked", Id);
            if (!HostExecutionVersionCurrent(version)) return;
            NeedActionCompleted(); // 0x005FBD26 / 0x005FBD2E.
            if (!HostExecutionVersionCurrent(version)) return;
        }
        if (!State(Phase.Complete, "Complete")) return;
        _exposureTimestamp = 0;
        _everConfirmed = false;
        _observation = Observation.None;
        RestoreCamera(version);
    }

    private void RestoreCamera(long version)
    {
        if (_cameraBeforeRun is not { } previous) return;
        _cameraBeforeRun = null;
        if (!HostExecutionVersionCurrent(version)) return;
        _m.Vision.QueueNativeSchedule(null);
        var camera = Facade.CameraSettings;
        camera.SetAndDisableAutoExposure((int)unchecked((ushort)previous.Exposure), previous.Gain);
        if (!HostExecutionVersionCurrent(version)) return;
        camera.EnableAutoExposure(true);
        if (HostExecutionVersionCurrent(version)) camera.EnableColorImages(previous.Color); // 0x005FBD90..0x005FBDB6.
    }

    private void Info(string key, string message) =>
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Behaviors", "BehaviorTrackLaser." + key, message);
    private void Debug(string key, string message) =>
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug, "Behaviors", "BehaviorTrackLaser." + key, message);
    private void Event(string name, float? value = null, string? data = null) =>
        _robot.Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.laser_behavior." + name,
            value?.ToString("F6", CultureInfo.InvariantCulture) ?? "", data is null ? null : new Dictionary<string, string> { ["$data"] = data });

    private static uint Unsigned(float value) => value > 0f ? value >= 4294967296f ? uint.MaxValue : (uint)value : 0;

    public void Dispose()
    {
        if (_retired) return;
        RestoreCamera(_generation);
        _retired = true;
        _imageSubscription?.Dispose();
        _laserSubscription?.Dispose();
        RetireActionCompletionOwner();
    }
}
