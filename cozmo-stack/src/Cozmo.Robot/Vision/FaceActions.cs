using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

/// <summary>Outcomes of the face actions (a subset of the engine's <c>ActionResult</c>, UNITY values).</summary>
public enum FaceActionResult : uint { Success = 0, Cancelled = 0x02000000, Abort = 0x03000000, NoFace = 0x0300000B, VisualObservationFailed = 0x0300001D, Timeout = 0x03000018 }

/// <summary>
/// The engine's <c>TurnTowardsPoseAction</c> (0x00549F10, a <c>PanAndTiltAction</c>): <c>Init</c> takes the pose
/// with respect to the robot (assuming the robot's origin as parent when it has none), the body turn is
/// <c>atan2(y, x)</c> and is skipped (the action still succeeds, only the head moves) when it exceeds the
/// maximum turn angle; the head angle comes from <c>Robot::ComputeHeadAngleToSeePose</c> (iterative, 0.01
/// tolerance, up to 25 iterations) clamped to −0.436332..0.776672. The default pan tolerance is 5°
/// (0x3DB2B8C2). <c>CheckIfDone</c> is the pan-and-tilt's. This stack sends <c>SetBodyAngle</c> and
/// <c>SetHeadAngle</c> through <see cref="TurnTowardsPose"/> and waits for the pose to settle.
/// </summary>
public sealed class TurnTowardsPoseAction
{
    public const double DefaultPanToleranceRad = 0.0872665;
    private readonly VisionSystem _v;
    public TurnTowardsPoseAction(VisionSystem v, Pose3d pose, double maxTurnAngleRad = Math.PI) { _v = v; Pose = pose; MaxTurnAngleRad = maxTurnAngleRad; }
    public Pose3d Pose { get; }
    public double MaxTurnAngleRad { get; }
    public double? RelativeTurnRad { get; private set; }
    public double? HeadAngleRad { get; private set; }

    public async Task<FaceActionResult> RunAsync(CancellationToken cancel)
    {
        var robot = _v.History.Latest?.RobotPose;
        if (robot is null) return FaceActionResult.Abort;
        RelativeTurnRad = TurnTowardsPose.RelativeTurnRad(robot.Value, Pose);
        if (_v.Calibration is { } cal) HeadAngleRad = Math.Clamp(TurnTowardsPose.HeadAngleToSee(cal, robot.Value, Pose.Translation), HeadGeometry.MinHeadAngleRad, HeadGeometry.MaxHeadAngleRad);
        bool ok = await FaceTurns.TurnAsync(_v, Pose, Math.Abs(RelativeTurnRad.Value) > MaxTurnAngleRad ? 0 : MaxTurnAngleRad, cancel);
        return ok || Math.Abs(RelativeTurnRad.Value) > MaxTurnAngleRad ? FaceActionResult.Success : cancel.IsCancellationRequested ? FaceActionResult.Cancelled : FaceActionResult.Timeout;
    }
}

/// <summary>
/// The engine's <c>TurnTowardsImagePointAction</c> 0x0054B59C: a <c>PanAndTiltAction</c> whose two angles
/// come from the pixel itself, with no distance anywhere in it.
///
/// <c>Robot::ComputeTurnTowardsImagePointAngles</c> 0x0051879C subtracts the calibration's centre from the
/// point (the two-float loop at 0x005187CC), takes the historical state at the image's timestamp, and then
/// computes <c>atan2(-(u - cx), fx)</c> for the body and <c>atan2(-(v - cy), fy)</c> for the head
/// (0x0051886C and 0x0051888E, the focal lengths read from the calibration at +4 and +8). The head angle
/// is added to the head angle in that historical state and the body angle to its heading, so both come out
/// absolute; <c>Init</c> 0x0054B664 writes them into the PanAndTilt fields at +0x114 and +0x11C and runs
/// the pan and tilt. When the history cannot answer, the action warns
/// "TurnTowardsImagePointAction.Init.ComputeTurnTowardsImagePointAnglesFailed" and does not turn.
/// </summary>
public static class TurnTowardsImagePoint
{
    /// <summary>
    /// The absolute body and head angles for a point in the image, given the calibration and the robot
    /// state the image was taken in.
    /// </summary>
    public static (double BodyRad, double HeadRad) Angles(CameraCalibration cal, double u, double v,
                                                          double headingRad, double headAngleRad)
    {
        double du = u - cal.CenterX, dv = v - cal.CenterY;
        return (headingRad + Math.Atan2(-du, cal.FocalLengthX),
                headAngleRad + Math.Atan2(-dv, cal.FocalLengthY));
    }

    /// <summary>Turns to those angles, the way the action's PanAndTilt does.</summary>
    public static async Task<bool> RunAsync(VisionSystem v, double u, double v_, CancellationToken cancel)
    {
        if (v.Calibration is not { } cal) return false;
        if (v.History.Latest is not { } state) return false;
        var (body, head) = Angles(cal, u, v_, state.RobotPose.AngleAroundZ, state.HeadAngleRad);
        head = Math.Clamp(head, HeadGeometry.MinHeadAngleRad, HeadGeometry.MaxHeadAngleRad);
        return await PanAndTilt.RunAsync(v, body, head, TurnTowardsPose.MaxSpeedRadPerSec, cancel);
    }
}

/// <summary>The body-and-head turn the face actions use, replaceable through <see cref="VisionSystem.TurnOverride"/> for tests.</summary>
public static class FaceTurns
{
    public static async Task<bool> TurnAsync(VisionSystem v, Pose3d target, double maxTurnAngleRad, CancellationToken cancel)
    {
        if (v.TurnOverride is not null) return await v.TurnOverride(target, maxTurnAngleRad, cancel);
        if (maxTurnAngleRad <= 0)
        {
            // head only
            var robot = v.History.Latest?.RobotPose;
            if (robot is null || v.Calibration is null) return false;
            double head = Math.Clamp(TurnTowardsPose.HeadAngleToSee(v.Calibration, robot.Value, target.Translation), HeadGeometry.MinHeadAngleRad, HeadGeometry.MaxHeadAngleRad);
            v.Robot.SendMessage(new SetHeadAngle { AngleRad = (float)head, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = 3 }, flush: true);
            return true;
        }
        return await TurnTowardsPose.RunAsync(v, target, maxTurnAngleRad, cancel);
    }
}

/// <summary>
/// The engine's <c>TurnTowardsFaceAction(robot, faceId, maxTurnAngle, sayName)</c> (0x0054B754..0x0054C780).
/// <c>Init</c>: the face's pose from <c>FaceWorld::GetFace</c>, or <c>GetLastObservedFace</c> when the id is
/// invalid ("Required face pose, don't have one, failing" without either), then <c>TurnTowardsPoseAction::Init</c>.
/// <c>CheckIfDone</c>: once the turn completes, if a <c>RobotObservedFace</c> for the face (any face when the id
/// was invalid) arrived meanwhile ("Observed ID=%s at distSq=%.1f"), <c>CreateFineTuneAction</c> fires the
/// emotion event "LookAtFaceVerified", registers the needs action, and turns again to the observed pose with
/// a 45° (0.785398) maximum; otherwise a <c>WaitForImagesAction</c> ("Will wait no more than %d frames") in
/// face-detection mode gives the tracker a chance, and the action completes without fine tuning. Then, when
/// asked to say the name: a named face gets <c>SayTextAction(name)</c> with the say-name trigger (unless
/// 0x23F, none), an unnamed one the no-name trigger through <c>TriggerLiftSafeAnimationAction</c>; finally
/// <c>FaceWorld::SetTurnedTowardsFace(id, true)</c>. INFERRED: the frame count waited (5).
/// </summary>
public sealed class TurnTowardsFaceAction : IDisposable
{
    /// <summary>Releases the smart face id's subscription to the face world.</summary>
    public void Dispose() => FaceId.Dispose();

    public const double FineTuneMaxTurnRad = 0.785398;
    /// <summary>
    /// How many frames the action will wait for the face to be seen: 10.
    /// <c>TurnTowardsFaceAction</c>'s constructor writes it at +0x188 (<c>movs r1, #0xa</c> at 0x0054B798),
    /// and <c>IVisuallyVerifyAction</c>'s writes the same 10 at +0x8C (0x0054B79E's counterpart at
    /// 0x0056873E), which <c>VisuallyVerifyFaceAction</c> 0x00568EB8 does not override - it passes only
    /// its vision mode and lift preset. This stack used 5, which was a guess.
    /// </summary>
    public const int FramesToWaitForFace = 10;
    private readonly VisionSystem _v;

    public TurnTowardsFaceAction(VisionSystem v, int faceId, double maxTurnAngleRad = Math.PI, bool sayName = false)
    {
        _v = v; FaceId = v.Faces.GetSmartFaceID(faceId); MaxTurnAngleRad = maxTurnAngleRad; SayName = sayName;
    }

    public SmartFaceID FaceId { get; }
    public double MaxTurnAngleRad { get; }
    public bool SayName { get; }
    /// <summary>The animation for a named / unnamed face when saying the name (<c>SetSayNameAnimationTrigger</c> / <c>SetNoNameAnimationTrigger</c>); null (the engine's 0x23F = Count) plays nothing.</summary>
    public AnimationTrigger? SayNameTrigger { get; set; }
    public AnimationTrigger? NoNameTrigger { get; set; }
    /// <summary>What the caller should play after the turn: the trigger chosen (null: say the name without one) and, for a named face, the name to say.</summary>
    public (AnimationTrigger? Trigger, string? NameToSay)? Reaction { get; private set; }
    public bool FineTuned { get; private set; }
    public bool ObservedFace { get; private set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    /// <summary>Fired with the emotion event name the engine triggers (the behaviour's mood applies it).</summary>
    public event Action<string>? EmotionEvent;

    public async Task<FaceActionResult> RunAsync(CancellationToken cancel)
    {
        var face = _v.Faces.GetFace(FaceId) ?? (FaceId.IsValid ? null : _v.Faces.GetLastObservedFace());
        if (face is null) { _trace.Add("TurnTowardsFaceAction.Init.NoFacePose: Required face pose, don't have one, failing"); return FaceActionResult.NoFace; }
        uint startTs = _v.History.Latest?.Timestamp ?? 0;
        var turn = new TurnTowardsPoseAction(_v, face.HeadPose, MaxTurnAngleRad);
        var r = await turn.RunAsync(cancel);
        _trace.Add($"turned towards face {face.Id}: {r} (body {turn.RelativeTurnRad * 180 / Math.PI:F0} deg, head {turn.HeadAngleRad * 180 / Math.PI:F0} deg)");
        if (r != FaceActionResult.Success) return r;
        // was the face observed since the turn began? if not, wait a few frames
        var seen = ObservedSince(face.Id, startTs);
        if (seen is null)
        {
            _trace.Add($"TurnTowardsFaceAction.CheckIfDone.NoFaceObservedYet: Will wait no more than {FramesToWaitForFace} frames");
            int target = _v.FramesProcessed + FramesToWaitForFace;
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (_v.FramesProcessed < target && DateTime.UtcNow < deadline && !cancel.IsCancellationRequested)
            {
                await Task.Delay(20, CancellationToken.None);
                if ((seen = ObservedSince(face.Id, startTs)) is not null) break;
            }
        }
        if (seen is not null)
        {
            ObservedFace = true;
            _trace.Add($"TurnTowardsFaceAction.CreateFinalAction.SawFace: Observed ID={seen.Id}. Will fine tune.");
            EmotionEvent?.Invoke("LookAtFaceVerified");
            var fine = new TurnTowardsPoseAction(_v, seen.HeadPose, FineTuneMaxTurnRad);
            await fine.RunAsync(cancel);
            FineTuned = true;
            face = seen;
        }
        if (SayName)
        {
            if (face.HasName) Reaction = (SayNameTrigger, face.Name);
            else if (NoNameTrigger is { } t) Reaction = (t, null);
        }
        _v.Faces.SetTurnedTowardsFace(face.Id, true);
        return FaceActionResult.Success;
    }

    private FaceEntry? ObservedSince(int id, uint ts)
    {
        var f = FaceId.IsValid ? _v.Faces.GetFace(FaceId) : _v.Faces.GetLastObservedFace();
        return f is not null && f.LastObservedTimestamp > ts ? f : null;
    }
}

/// <summary>
/// The engine's <c>TrackFaceAction</c> (0x00565ADC, an <c>ITrackAction</c>): every update takes the face's pose
/// with respect to the robot and asks for pan = atan2(y, x) + the robot's heading (absolute) and
/// tilt = atan((z − 49) / planar distance) (0xC2440000: the neck height), within the pan/tilt tolerances
/// (minimum 2°, 0x3D0EFA35; the face behaviours set 4°, 0.0698132), for a duration or until stopped; the
/// face's id follows <c>RobotChangedObservedFaceID</c> ("Updating tracked face ID from %d to %d").
/// <c>ITrackAction</c> also clamps small angles to the tolerances for random periods when asked
/// (<c>SetClampSmallAnglesToTolerances</c>, period 0.4/0.15 default) and shifts the eyes (±32/±16 px). The eye
/// shift and driving animation are DEFERRED. Update period LOCAL (100 ms).
/// </summary>
public sealed class TrackFaceAction : IDisposable
{
    public const double NeckHeightMm = 49.0;

    /// <summary>
    /// The pan and tilt tolerances an <c>ITrackAction</c> starts with, 0.0349066 rad (2 degrees), written
    /// into the action at +0x84 and +0x8C by its constructor (0x005646BC and 0x005646CC).
    /// </summary>
    public const double MinToleranceRad = 0.0349066;

    /// <summary>
    /// How long the body turn is given, 0.4 s: the constructor's <c>strd r1, r0, [r4, #0xd0]</c> at
    /// 0x00564758 puts 0.15 at +0xD0 and 0.4 at +0xD4, and <c>SetPanDuration</c> 0x00564AD4 writes +0xD4.
    /// </summary>
    public const double PanDurationSec = 0.4;

    /// <summary>The head's, 0.15 s, from the same pair - <c>SetTiltDuration</c> 0x00564ADA writes +0xD0.</summary>
    public const double TiltDurationSec = 0.15;

    /// <summary>
    /// The acceleration a tracking turn asks for: 10000, the immediate one
    /// (<c>movt r3, #0x461c</c> at 0x005650EE).
    /// </summary>
    public const double TrackAccelRadPerSec2 = 10000;

    /// <summary>
    /// How high the head may go while tracking, 0.776672 rad, at +0x94 (0x005646DC).
    /// </summary>
    public const double MaxHeadAngleRad = 0.776672;

    /// <summary>
    /// The turn a sound needs before it plays, 0.174533 rad (10 degrees) for both axes, at +0xC0 and
    /// +0xC8 (0x00564722 and 0x00564732). No sound is set by default.
    /// </summary>
    public const double MinAngleForSoundRad = 0.174533;

    /// <summary>
    /// The time the action aims to reach the target in, 0.5 s, at +0xD8 (0x0056475C);
    /// <c>SetDesiredTimeToReachTarget</c> 0x00564AE4 writes it.
    /// </summary>
    public const double DesiredTimeToReachTargetSec = 0.5;

    /// <summary>
    /// The action tick. The engine's tracking runs inside <c>CheckIfDone</c>, which the action list calls
    /// every basestation tick; there is no update period of its own (its update timeout at +0x7C is not
    /// one, and the constructor leaves the three times at +0xE4..+0xEC at -1). This stack polled at 100 ms,
    /// which was invented.
    /// </summary>
    public const int UpdateIntervalMs = 33;
    private readonly VisionSystem _v;
    public TrackFaceAction(VisionSystem v, int faceId) { _v = v; FaceId = v.Faces.GetSmartFaceID(faceId); }
    public void Dispose() => FaceId.Dispose();
    public SmartFaceID FaceId { get; }
    public double PanToleranceRad { get; set; } = MinToleranceRad;
    public double TiltToleranceRad { get; set; } = MinToleranceRad;

    /// <summary>
    /// Whether the eyes shift towards the target as well as the head turning: <c>SetMoveEyes</c>
    /// 0x00564B40 writes the byte at +0xA1, which the constructor zeroes (the <c>strh</c> at 0x0056470E),
    /// so it is off unless something asks for it. Nothing this stack drives asks.
    /// </summary>
    public bool MoveEyes { get; set; }

    /// <summary>
    /// Whether the driving animation plays around the turn: <c>EnableDrivingAnimation</c> 0x00564AEA
    /// writes +0xA8, which the constructor zeroes (0x00564716), and <c>CheckIfDone</c> only calls
    /// <c>DrivingAnimationHandler::PlayEndAnim</c> when it is set (0x0056510E).
    /// </summary>
    public bool DrivingAnimation { get; set; }
    public int Updates { get; private set; }
    public int Turns { get; private set; }
    public (double Pan, double Tilt)? LastCommand { get; private set; }

    /// <summary>Tracks for the duration (or until cancelled); false when the face was lost.</summary>
    public async Task<bool> RunAsync(TimeSpan duration, CancellationToken cancel)
    {
        var end = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < end && !cancel.IsCancellationRequested)
        {
            var face = _v.Faces.GetFace(FaceId);
            var robot = _v.History.Latest?.RobotPose;
            if (face is null || robot is null) return false;
            Updates++;
            var rel = face.HeadPose.WithRespectTo(robot.Value).Translation;
            double dist = Math.Sqrt(rel.X * rel.X + rel.Y * rel.Y);
            double pan = Math.Atan2(rel.Y, rel.X);
            double tilt = Math.Atan2(rel.Z - NeckHeightMm, dist);
            double headNow = _v.History.Latest?.HeadAngleRad ?? 0;
            if (Math.Abs(pan) > PanToleranceRad || Math.Abs(tilt - headNow) > TiltToleranceRad)
            {
                Turns++;
                double targetHead = Math.Clamp(tilt, HeadGeometry.MinHeadAngleRad, Math.Min(MaxHeadAngleRad, HeadGeometry.MaxHeadAngleRad));
                LastCommand = (robot.Value.AngleAroundZ + pan, targetHead);
                // The tracking tilt is the one computed just above, atan((z − 49) / planar distance). Handing
                // the head pose to the generic look-at solve instead would recompute a different head angle
                // and throw this away, which is not what the recovered TrackFaceAction does.
                //
                // The speeds are the engine's: each axis is given its own duration to cover the angle it
                // has to cover, so the speed is |delta| / duration and the acceleration is the immediate
                // 10000 (MoveHeadToAngle at 0x00565104 with the speed computed at 0x005650F2). Nothing
                // waits for the turn to settle - the next tick recomputes the target.
                double headSpeed = Math.Max(0.01, Math.Abs(targetHead - headNow) / TiltDurationSec);
                double bodySpeed = Math.Max(0.01, Math.Abs(pan) / PanDurationSec);
                await PanAndTilt.RunAsync(_v, LastCommand.Value.Pan, LastCommand.Value.Tilt, bodySpeed, cancel,
                                          headSpeed, TrackAccelRadPerSec2, waitForSettle: false);
            }
            await Task.Delay(UpdateIntervalMs, CancellationToken.None);
        }
        return true;
    }
}

/// <summary>
/// <c>VisuallyVerifyFaceAction(robot, faceId)</c> 0x00568EB8: waits for an observation of the face within
/// <see cref="TurnTowardsFaceAction.FramesToWaitForFace"/> frames, the 10 its base
/// <c>IVisuallyVerifyAction</c> puts at +0x8C, as <c>VisuallyVerifyObjectAction</c> does for objects.
/// </summary>
public sealed class VisuallyVerifyFaceAction
{
    private readonly VisionSystem _v;
    public VisuallyVerifyFaceAction(VisionSystem v, int faceId) { _v = v; FaceId = faceId; }
    public int FaceId { get; }

    public async Task<FaceActionResult> RunAsync(CancellationToken cancel)
    {
        uint start = _v.History.Latest?.Timestamp ?? 0;
        int target = _v.FramesProcessed + TurnTowardsFaceAction.FramesToWaitForFace;
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && !cancel.IsCancellationRequested)
        {
            var f = _v.Faces.GetFace(FaceId);
            if (f is not null && f.LastObservedTimestamp > start) return FaceActionResult.Success;
            if (_v.FramesProcessed >= target) break;
            await Task.Delay(20, CancellationToken.None);
        }
        return FaceActionResult.VisualObservationFailed;
    }
}
