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
            v.Robot.Transport.Send(new SetHeadAngle { AngleRad = (float)head, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = 3 }, flush: true);
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
public sealed class TurnTowardsFaceAction
{
    public const double FineTuneMaxTurnRad = 0.785398;
    public const int FramesToWaitForFace = 5;
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
public sealed class TrackFaceAction
{
    public const double NeckHeightMm = 49.0;
    public const double MinToleranceRad = 0.0349066;
    private readonly VisionSystem _v;
    public TrackFaceAction(VisionSystem v, int faceId) { _v = v; FaceId = v.Faces.GetSmartFaceID(faceId); }
    public SmartFaceID FaceId { get; }
    public double PanToleranceRad { get; set; } = MinToleranceRad;
    public double TiltToleranceRad { get; set; } = MinToleranceRad;
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
                LastCommand = (robot.Value.AngleAroundZ + pan, tilt);
                await FaceTurns.TurnAsync(_v, face.HeadPose, Math.PI, cancel);
            }
            await Task.Delay(100, CancellationToken.None);
        }
        return true;
    }
}

/// <summary>
/// <c>VisuallyVerifyFaceAction(robot, faceId)</c>: waits for an observation of the face within a few frames
/// (as <c>VisuallyVerifyObjectAction</c> does for objects). INFERRED: the frame budget (5).
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
