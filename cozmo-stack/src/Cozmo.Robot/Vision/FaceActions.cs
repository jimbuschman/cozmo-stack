using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

/// <summary>Outcomes of the face actions (a subset of the engine's <c>ActionResult</c>, UNITY values).</summary>
public enum FaceActionResult : uint { Success = 0, Cancelled = 0x02000000, Abort = 0x03000000,
    /// <summary>0x0300000B is the engine's MISMATCHED_UP_AXIS, not NO_FACE, and no TurnTowardsFaceAction path returns it (M13-014, R-VIS gap 2 Q2). Kept only because the enum is public; nothing in this stack produces it.</summary>
    NoFace = 0x0300000B,
    VisualObservationFailed = 0x0300001D, Timeout = 0x03000018,
    /// <summary>BAD_POSE 0x03000005: what <c>TurnTowardsPoseAction::Init</c> returns for an unset or unreachable pose (M13-020).</summary>
    BadPose = 0x03000005,
    /// <summary>NO_FACE 0x0300000E: what a <see cref="TurnTowardsFaceAction"/> whose +0x193 byte is set returns when no face pose is found or it ends without a verified face (M13-014).</summary>
    NoFaceRequired = 0x0300000E }

/// <summary>
/// The engine's <c>TurnTowardsPoseAction</c> (0x00549F10, a <c>PanAndTiltAction</c>) as this stack runs it. <c>Init</c> is
/// <see cref="TurnTowardsPoseCompound.InitPose"/> (M13-020, built in full): BAD_POSE for an unset or unreachable pose, the body
/// turn <c>atan2(y, x)</c> when the maximum turn is positive and the pan is within it, otherwise <c>+0x179 = 1</c> and NOTHING moves
/// (no body turn and no head move), and the head angle from <c>Robot::ComputeHeadAngleToSeePose</c> clamped to -0.436332..0.776672.
/// <c>CheckIfDone</c> returns 0 when <c>+0x179</c> is set. The compound the engine then runs (a TurnInPlaceAction and a
/// MoveHeadToAngleAction under a CompoundActionParallel) is not connected (M13-020/M13-022): this stack sends the body and head
/// through <see cref="FaceTurns"/> and <see cref="TurnTowardsPose"/> (M11-014/M11-015), and waits for the pose to settle.
///
/// The head angle is <c>Robot::ComputeHeadAngleToSeePose(pose, &amp;rad, 0.01f)</c> (0x00518344, <see cref="TurnTowardsPose.ComputeHeadAngleToSeePose"/>: the engine's 25-iteration loop in binary32 over this
/// stack's double pose algebra, MISSING for the last bits). When it returns non-zero (no calibration, a projected z at or behind the camera, a loop that ends on its 25th pass) Init logs
/// "TurnTowardsPoseAction.Init.FailedToComputedHeadAngle" (0x0054AB66) and falls back to <c>GetAbsoluteHeadAngleToLookAtPose</c> (0x0054B428, <see cref="TurnTowardsPoseCompound.AbsoluteHeadAngleToLookAtPose"/>)
/// with the robot-frame translation. With no robot pose yet the result is BAD_POSE (the engine always has one).
/// </summary>
// fidelity: M13-020
public sealed class TurnTowardsPoseAction
{
    public const double DefaultPanToleranceRad = 0.0872665;
    private readonly VisionSystem _v;
    private readonly TurnTowardsPoseCompound _compound;
    private bool _inited;
    public TurnTowardsPoseAction(VisionSystem v, Pose3d pose, double maxTurnAngleRad = Math.PI) { _v = v; Pose = pose; MaxTurnAngleRad = maxTurnAngleRad; _compound = new TurnTowardsPoseCompound(pose, maxTurnAngleRad); }
    public Pose3d Pose { get; }
    public double MaxTurnAngleRad { get; }
    public double? RelativeTurnRad { get; private set; }
    public double? HeadAngleRad { get; private set; }
    /// <summary><c>+0x179</c>: the pan exceeded the maximum turn, so Init returned 0 and nothing moves.</summary>
    public bool TurnSkipped => _compound.Byte0x179;

    /// <summary><c>TurnTowardsPoseAction::Init</c> (through <c>ComputeHeadAngleToSeePose</c>); returns the ActionResult code.</summary>
    public uint Init()
    {
        _inited = true;
        var latest = _v.History.Latest;
        if (latest is null) return TurnTowardsPoseCompound.BadPose;
        var robot = latest.Value.RobotPose;
        var env = new TurnTowardsPoseEnv(robot, latest.Value.HeadAngleRad,
            p => TurnTowardsPose.ComputeHeadAngleToSeePose(_v.Calibration, p, TurnTowardsPoseCompound.SeePoseToleranceRad, out var seen, _v.LogLine) == 0 ? seen : null,   // Robot::ComputeHeadAngleToSeePose(pose, &rad, 0.01f); non-zero is the engine's failure result
            null, null, _v.LogLine);
        uint r = _compound.InitPose(env, out _);
        RelativeTurnRad = _compound.PanAngleRad;
        if (r == 0 && !_compound.Byte0x179) HeadAngleRad = _compound.HeadAngleRad;
        return r;
    }

    public async Task<FaceActionResult> RunAsync(CancellationToken cancel)
    {
        uint init = _inited ? 0 : Init();
        if (init != 0) return (FaceActionResult)init;
        return await ExecuteAsync(cancel);
    }

    /// <summary>The turn after a successful <see cref="Init"/>: <c>CheckIfDone</c> 0x0054B011 is 0 when +0x179 is set, else the compound (here the live turn) runs.</summary>
    public async Task<FaceActionResult> ExecuteAsync(CancellationToken cancel)
    {
        if (_compound.Byte0x179) return FaceActionResult.Success;
        bool ok = await FaceTurns.TurnAsync(_v, Pose, _compound.MaxTurnAbsRad, cancel);
        return ok ? FaceActionResult.Success : cancel.IsCancellationRequested ? FaceActionResult.Cancelled : FaceActionResult.Timeout;
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
// fidelity: M14-005
public static class TurnTowardsImagePoint
{
    /// <summary>
    /// The absolute body and head angles for a point in the image, given the calibration and the robot
    /// state the image was taken in.
    /// </summary>
    /// <remarks>
    /// binary32 as <c>Robot::ComputeTurnTowardsImagePointAngles</c> 0x0051879C computes it: <c>du = u - cx</c>, <c>dv = v - cy</c> (<c>vsub.f32</c>,
    /// 0x005187E2); head = <c>atan2f(-dv, fy) + headAngle</c> (<c>vadd.f32</c> 0x00518878) assigned through <c>Radians::operator=(float)</c>; body =
    /// <c>operator+(float, Radians)</c> 0x0084C97B: <c>Radians(atan2f(-du, fx))</c> plus the heading, one <c>vadd.f32</c> (0x0084C99C), rescaled.
    /// MISSING (libm f32 stand-in): the engine calls bionic <c>atan2f</c> (PLT 0x4A4510); <c>MathF.Atan2</c> is not claimed bit-equal to it.
    /// The rescale is <see cref="EngineRadians.Rescale"/> (double arithmetic on a binary32 value; the engine's 0x0084C87C is binary32).
    /// </remarks>
    public static (double BodyRad, double HeadRad) Angles(CameraCalibration cal, double u, double v,
                                                          double headingRad, double headAngleRad)
    {
        float du = (float)u - (float)cal.CenterX, dv = (float)v - (float)cal.CenterY;
        float headDelta = MathF.Atan2(-dv, (float)cal.FocalLengthY);       // 0x0051886C
        float head = headDelta + (float)headAngleRad;                        // 0x00518878
        float bodyDelta = (float)EngineRadians.Rescale(MathF.Atan2(-du, (float)cal.FocalLengthX));   // 0x0051888E, Radians(float)
        float body = bodyDelta + (float)headingRad;                          // 0x0084C99C
        return (EngineRadians.Rescale(body), EngineRadians.Rescale(head));
    }

    /// <summary>
    /// Turns to those angles, the way the action's PanAndTilt does. The historical state is the one at
    /// the image's own timestamp (F5, <c>RobotStateHistory::ComputeStateAt</c>); when it cannot answer the
    /// action warns <c>ComputeTurnTowardsImagePointAnglesFailed</c> and turns nowhere.
    /// </summary>
    public static async Task<bool> RunAsync(VisionSystem v, double u, double v_, uint imageTimestamp, CancellationToken cancel)
    {
        if (v.Calibration is not { } cal) return false;
        if (v.History.At(imageTimestamp) is not { } state)
        {
            v.LogLine("TurnTowardsImagePointAction.Init.ComputeTurnTowardsImagePointAnglesFailed");
            return false;
        }
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
            // fidelity: M4-005
            // MA8: the direct SetHeadAngle takes the shared u8 counter (MC+8), pre-incremented.
            v.Robot.SendMessage(new SetHeadAngle { AngleRad = (float)head, MaxSpeedRadPerSec = 10f, AccelRadPerSec2 = 10f, DurationSec = 0f, ActionId = v.Robot.Motion.NextActionId() }, flush: true);
            return true;
        }
        return await TurnTowardsPose.RunAsync(v, target, maxTurnAngleRad, cancel);
    }
}

/// <summary>
/// The engine's <c>TurnTowardsFaceAction(robot, faceId, maxTurnAngle, sayName)</c> (0x0054B754..0x0054C780), M13-014 (read in R-VIS gap
/// pass 2, Q2/Q3/Q6). The face id 0 is the invalid <c>SmartFaceID</c> ("no face"); its <c>Init</c> takes the pose of
/// <c>FaceWorld::GetFace</c> for a valid id, or of <c>FaceWorld::GetLastObservedFace(pose, false)</c> for an invalid one; with no pose it
/// logs "Required face pose, don't have one, failing" and returns NO_FACE 0x0300000E only when the +0x193 byte
/// (<see cref="RequireVerifiedFace"/>) is set, and with it clear sets state 3 and returns SUCCESS (never 0x0300000B). A success
/// clears the verified id (+0x18C), sets the best distance to FLT_MAX, subscribes to <c>RobotObservedFace</c>, locks tracks 5, and
/// returns <c>TurnTowardsPoseAction::Init</c>.
///
/// The <c>RobotObservedFace</c> handler (0x0054C050..0x0054C19E), while the state is 0 or 1 (during the turn and the wait): for a valid
/// id it sets the verified id to it when the message's face matches; for an invalid id it takes the face's pose with respect to the
/// robot and, if its 3-D distance squared is strictly below the best so far, makes that face the verified id. "Verified" is that
/// <c>SmartFaceID</c> being valid.
///
/// <c>CheckIfDone</c> states: 0 turning (then the fine tune when a face is verified, else a <c>WaitForImagesAction</c> of 10 frames,
/// vision mode 2, and state 1); 1 waiting (a verified face starts the fine tune; when the wait ends with none, +0x193 set returns
/// 0x0300000E, else success); 2 the fine tune, a <c>TurnTowardsPoseAction</c> with <c>min(|maxTurn|, 0.7853982)</c> after
/// <c>NeedsManager::RegisterNeedsActionCompleted(SeeFace 0x2E)</c> and the "LookAtFaceVerified" emotion event, then, when asked to say
/// the name, a named face gets <c>SayTextAction(name)</c> with the say-name function's trigger (none = 0x23F), an unnamed one the
/// no-name function's trigger through <c>TriggerLiftSafeAnimationAction</c>; 3 registers SayName 0x29; the final step calls
/// <c>FaceWorld::SetTurnedTowardsFace(verified id, true)</c> when the verified id is valid.
///
/// What is a call-site stub and stays visible: <c>MovementComponent::LockTracks/UnlockTracks</c> (bodies unread, M13-021: noted in
/// <see cref="Trace"/>, nothing is sent), <c>NeedsManager</c> registrations (recorded in <see cref="NeedsActionsRegistered"/>; the manager
/// is another layer), the emotion event (<see cref="EmotionEvent"/>), and the SayText / TriggerLiftSafe children (the caller plays
/// <see cref="Reaction"/>). The turn itself is <see cref="TurnTowardsPoseAction"/>'s live executor, not the engine's compound.
///
/// CHOICES the inventory does not settle: a <see cref="SmartFaceID.Invalid"/> (-1, this stack's spelling of "no face" that the explorer
/// and face behaviours pass) is treated like the engine's id 0; the wait (10 frames) also ends after 2 s, a guard from the earlier
/// implementation (the record gives no time limit); a cancelled action returns <see cref="FaceActionResult.Cancelled"/> (the action
/// runner's, not the action's); the face's pose is taken as the robot-frame conversion of the world pose (one origin, so it cannot fail
/// while a robot pose exists, and a missing robot pose is the failure).
/// </summary>
// fidelity: M13-014, M13-021
public class TurnTowardsFaceAction : IDisposable
{
    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    /// <summary>Releases the smart face id's subscription to the face world.</summary>
    public void Dispose()
    {
        Unsubscribe();
        FaceId.Dispose();
        lock (_gate) _verified.Dispose();
    }

    /// <summary>The fine tune's maximum turn is <c>min(|maxTurn|, 0.7853982)</c>: 0x3F490FDB at 0x0054C4E4.</summary>
    public static readonly double FineTuneMaxTurnRad = BitConverter.Int32BitsToSingle(0x3F490FDB);
    // fidelity: M14-002
    /// <summary>
    /// How many frames the action will wait for the face to be seen: 10.
    /// <c>TurnTowardsFaceAction</c>'s constructor writes it at +0x188 (<c>movs r1, #0xa</c> at 0x0054B798),
    /// and <c>IVisuallyVerifyAction</c>'s writes the same 10 at +0x8C (0x0054B79E's counterpart at
    /// 0x0056873E), which <c>VisuallyVerifyFaceAction</c> 0x00568EB8 does not override - it passes only
    /// its vision mode and lift preset. This stack used 5, which was a guess.
    /// </summary>
    public const int FramesToWaitForFace = 10;
    /// <summary>NeedsActionId SeeFace, registered when the fine tune is created (0x0054C2E0..0x0054C2E6).</summary>
    public const uint NeedsActionSeeFace = 0x2E;
    /// <summary>NeedsActionId SayName, registered when state 3 completes (0x0054C616..0x0054C61E).</summary>
    public const uint NeedsActionSayName = 0x29;
    private readonly VisionSystem _v;
    private readonly object _gate = new();
    private SmartFaceID _verified;                        // +0x18C
    private float _bestDistSq = float.MaxValue;           // +0x184 (0x7F7FFFFF)
    private int _state;                                   // +0x190
    private bool _tracksLocked;                           // +0x192
    private bool _subscribed;
    private TurnTowardsPoseAction? _turn;                 // the state-0 turn
    private TurnTowardsPoseAction? _child;                // +0x180 after CreateFineTuneAction
    private Func<SmartFaceID, AnimationTrigger?>? _sayNameFn;   // +0x198 (pointer +0x1A8)
    private Func<SmartFaceID, AnimationTrigger?>? _noNameFn;    // +0x1B0 (pointer +0x1C0)
    private AnimationTrigger? _sayNameConst, _noNameConst;
    private readonly List<uint> _needs = new();

    public TurnTowardsFaceAction(VisionSystem v, int faceId, double maxTurnAngleRad = Math.PI, bool sayName = false)
    {
        _v = v; FaceId = v.Faces.GetSmartFaceID(faceId); MaxTurnAngleRad = maxTurnAngleRad; SayName = sayName;
        _verified = new SmartFaceID();
    }

    public SmartFaceID FaceId { get; }
    public double MaxTurnAngleRad { get; }
    public bool SayName { get; }
    /// <summary>+0x192: tracks locked by Init and unlocked when the turn ends (the MovementComponent calls are stubs, M13-021).</summary>
    public bool TracksLocked => _tracksLocked;
    /// <summary>+0x190.</summary>
    public int State { get { lock (_gate) return _state; } }
    /// <summary>The id +0x18C holds when it is valid (the "verified face"), else null.</summary>
    public int? VerifiedFaceId { get { lock (_gate) return IsValidId(_verified) ? _verified.Id : null; } }
    /// <summary>The NeedsManager registrations the engine makes (SeeFace 0x2E, SayName 0x29), in order.</summary>
    public IReadOnlyList<uint> NeedsActionsRegistered => _needs;

    /// <summary><c>SmartFaceID::IsValid</c> 0x0053B31A is Impl non-null and id != 0; this stack's <see cref="SmartFaceID.Invalid"/> (-1) is also "no face".</summary>
    private static bool IsValidId(SmartFaceID s) => s.IsValid && s.Id != 0;

    /// <summary>
    /// The byte at +0x193 (M13-014): zero from the constructor (0x0054B7B6) and written only by four behaviours
    /// (0x005C229C BehaviorInteractWithFaces with the constructor's sayName bool, 0x005DE170 BehaviorPyramidThankYou = 1,
    /// 0x005F20B8 BehaviorFistBump = 1, 0x005F6942 BehaviorPeekABoo = its +0x154 byte). Nonzero: the action returns
    /// <see cref="FaceActionResult.NoFaceRequired"/> when no face pose is found or it ends without a verified face.
    /// The engine's name for the field is not in the binary.
    /// </summary>
    // fidelity: M13-014
    public bool RequireVerifiedFace { get; set; }

    /// <summary>
    /// <c>SetSayNameTriggerCallback</c> 0x0054BB8C: logs "TurnTowardsFaceAction.SetSayNameTriggerCallbackWithoutSayingName" when sayName
    /// is 0 and, regardless of the flag, installs the function in the slot at this+0x198. The only caller in the engine is
    /// <c>BehaviorAcknowledgeFace</c> 0x00602A6A. The Robot argument of the engine's function type is not modelled.
    /// </summary>
    // fidelity: M13-014
    public void SetSayNameTriggerCallback(Func<SmartFaceID, AnimationTrigger?> callback)
    {
        if (!SayName) Note("TurnTowardsFaceAction.SetSayNameTriggerCallbackWithoutSayingName");
        _sayNameFn = callback;
    }

    /// <summary><c>SetNoNameTriggerCallback</c> 0x0054BC6C: the same on the slot at this+0x1B0 (its log text is the say-name copy).</summary>
    // fidelity: M13-014
    public void SetNoNameTriggerCallback(Func<SmartFaceID, AnimationTrigger?> callback)
    {
        if (!SayName) Note("TurnTowardsFaceAction.SetNoNameTriggerCallbackWithoutSayingName");
        _noNameFn = callback;
    }

    /// <summary>
    /// <c>SetSayNameAnimationTrigger</c> 0x0054B978: logs "...SetSayNameTriggerWithoutSayingName" when sayName is 0 and, regardless of the flag,
    /// installs a function returning the trigger in this+0x198. Null (the engine's 0x23F = Count) plays nothing.
    /// </summary>
    // fidelity: M13-014
    public AnimationTrigger? SayNameTrigger
    {
        get => _sayNameConst;
        set { if (!SayName) Note("TurnTowardsFaceAction.SetSayNameTriggerWithoutSayingName"); _sayNameConst = value; _sayNameFn = _ => value; }
    }

    /// <summary><c>SetNoNameAnimationTrigger</c> 0x0054BA84: the same on this+0x1B0 ("...SetNoNameTriggerWithoutSayingName").</summary>
    // fidelity: M13-014
    public AnimationTrigger? NoNameTrigger
    {
        get => _noNameConst;
        set { if (!SayName) Note("TurnTowardsFaceAction.SetNoNameTriggerWithoutSayingName"); _noNameConst = value; _noNameFn = _ => value; }
    }

    /// <summary>What the caller should play after the turn: the trigger chosen (null: say the name without one) and, for a named face, the name to say.</summary>
    public (AnimationTrigger? Trigger, string? NameToSay)? Reaction { get; private set; }
    public bool FineTuned { get; private set; }
    public bool ObservedFace { get; private set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    /// <summary>Fired with the emotion event name the engine triggers (the behaviour's mood applies it).</summary>
    public event Action<string>? EmotionEvent;

    private void Note(string line) { lock (_trace) _trace.Add(line); _v.LogLine(line); }

    /// <summary>
    /// <c>Init</c> (0x0054BD66..0x0054BEBC). Returns 0 (Success) with the state 0 turn ready, or with state 3 when the pose was not found and
    /// +0x193 is clear; NO_FACE 0x0300000E when it was not found and +0x193 is set; or what <c>TurnTowardsPoseAction::Init</c> returns.
    /// </summary>
    // fidelity: M13-014
    public FaceActionResult Init()
    {
        var robot = _v.History.Latest?.RobotPose;                                        // Robot::GetPose()
        Pose3d? pose = null;
        if (IsValidId(FaceId))                                                           // 0x0054BD66..0x0054BD9A
        {
            if (_v.Faces.GetFace(FaceId) is { } f && robot is not null) pose = f.HeadPose;   // GetFace null or GetWithRespectTo failing: silent
        }
        else if (_v.Faces.GetLastObservedFace() is { } last)                             // 0x0054BD9C..0x0054BE6A: GetLastObservedFace(pose, false)
        {
            if (robot is not null) pose = last.HeadPose;
            else Note("TurnTowardsFaceAction.Init.BadLastObservedFacePose: Could not get last observed face pose w.r.t. robot pose");
        }
        if (pose is null)
        {
            if (RequireVerifiedFace)                                                     // 0x0054BE6C..0x0054BEB4
            {
                Note("TurnTowardsFaceAction.Init.NoFacePose: Required face pose, don't have one, failing");
                return FaceActionResult.NoFaceRequired;
            }
            lock (_gate) _state = 3;                                                     // 0x0054BEB6..0x0054BEBC: +0x190 = 3, return 0
            return FaceActionResult.Success;
        }
        _child = null;                                                                   // 0x0054BDBA..0x0054BE34
        lock (_gate)
        {
            _verified.Dispose(); _verified = new SmartFaceID();                          // +0x18C.Reset()
            _bestDistSq = float.MaxValue;
            _state = 0;
        }
        Subscribe();                                                                     // tag 0x46 RobotObservedFace
        Note("MovementComponent::LockTracks(5, ...) 0x004F0F4C: body unread (M13-021), not sent");
        _tracksLocked = true;                                                            // +0x192 = 1
        _turn = new TurnTowardsPoseAction(_v, pose.Value, MaxTurnAngleRad);
        return (FaceActionResult)_turn.Init();                                           // return TurnTowardsPoseAction::Init()
    }

    private void Subscribe() { if (_subscribed) return; _subscribed = true; _v.Faces.FaceObserved += OnFaceObserved; }
    private void Unsubscribe() { if (!_subscribed) return; _subscribed = false; _v.Faces.FaceObserved -= OnFaceObserved; }
    private void OnFaceObserved(FaceObservation o) => HandleRobotObservedFace(o.Face.Id);

    /// <summary>The <c>RobotObservedFace</c> handler 0x0054C050..0x0054C19E.</summary>
    // fidelity: M13-014
    public void HandleRobotObservedFace(int messageFaceId)
    {
        lock (_gate)
        {
            if (_state > 1) return;                                                      // 0x0054C062
            if (IsValidId(FaceId))
            {
                if (FaceId.MatchesFaceID(messageFaceId)) SetVerified(FaceId.Id);         // +0x18C = +0x17C
                return;
            }
            if (_v.Faces.GetFace(messageFaceId) is not { } face) return;                 // null: ignore
            if (_v.History.Latest?.RobotPose is not { } robot) return;                   // GetWithRespectTo failing: ignore
            var rel = face.HeadPose.WithRespectTo(robot).Translation;
            float d2 = (float)(rel.X * rel.X + rel.Y * rel.Y + rel.Z * rel.Z);           // 3-D, +0x20/+0x24/+0x28
            if (!(d2 < _bestDistSq)) return;                                             // strict
            SetVerified(messageFaceId);                                                  // FaceWorld::UpdateSmartFaceToID
            _bestDistSq = d2;
            Note($"TurnTowardsFaceAction.ObservedFaceCallback: Observed ID={messageFaceId} at distSq={d2:F1}");
        }
    }

    private void SetVerified(int id) { _verified.Dispose(); _verified = _v.Faces.GetSmartFaceID(id); }

    public async Task<FaceActionResult> RunAsync(CancellationToken cancel)
    {
        try { return await RunStatesAsync(cancel); }
        finally { Unsubscribe(); }
    }

    private async Task<FaceActionResult> RunStatesAsync(CancellationToken cancel)
    {
        var init = Init();
        if (init != FaceActionResult.Success) return init;
        if (State == 3) return Final();                                                  // state 3, no child: CheckIfDone's final step
        // state 0 (0x0054C518..0x0054C550): the turn
        var r5 = _turn!.TurnSkipped ? FaceActionResult.Success : await _turn.ExecuteAsync(cancel);
        Note("MovementComponent::UnlockTracks(5, ...): body unread (M13-021), not sent");
        _tracksLocked = false;                                                           // +0x192 = 0
        Note($"turned towards face: {r5} (body {_turn.RelativeTurnRad * 180 / Math.PI:F0} deg, head {_turn.HeadAngleRad * 180 / Math.PI:F0} deg{(_turn.TurnSkipped ? ", turn skipped: pan beyond the maximum" : "")})");
        if (r5 != FaceActionResult.Success) return r5;
        if (VerifiedFaceId is null)                                                      // 0x0054C640..0x0054C6B8
        {
            Note($"TurnTowardsFaceAction.CheckIfDone.NoFaceObservedYet: Will wait no more than {FramesToWaitForFace} frames");
            lock (_gate) _state = 1;                                                     // WaitForImagesAction(robot, 10, VisionMode 2, 0)
            int target = _v.FramesProcessed + FramesToWaitForFace;
            var deadline = UtcNow().AddSeconds(2);                                // local guard, see the summary
            while (VerifiedFaceId is null && _v.FramesProcessed < target && UtcNow() < deadline && !cancel.IsCancellationRequested)
                await Task.Delay(20, CancellationToken.None);
            if (VerifiedFaceId is null)                                                  // state 1, 0x0054C5DA..0x0054C604
            {
                if (cancel.IsCancellationRequested) return FaceActionResult.Cancelled;
                if (RequireVerifiedFace) return FaceActionResult.NoFaceRequired;         // 0x0054C5F6
                return Final();                                                          // r5 == 0: the final step
            }
        }
        CreateFineTuneAction();                                                          // state 2
        if (_child is null) return Final();
        var fine = await _child.RunAsync(cancel);
        if (fine != FaceActionResult.Success) return fine;                               // nonzero is returned
        FineTuned = true;
        if (!SayName) return Final();                                                    // 0x0054C552..0x0054C60A
        var face = VerifiedFaceId is { } vid ? _v.Faces.GetFace(vid) : null;
        if (face is null) return Final();
        if (face.HasName)
        {
            Reaction = (_sayNameFn?.Invoke(_verified), face.Name);                       // SayTextAction(name, intent 3) [+ the say-name function's trigger]
        }
        else
        {
            if (_noNameFn is null) return Final();
            if (_noNameFn(_verified) is not { } trigger) return Final();                 // 0x23F: none
            Reaction = (trigger, null);                                                  // TriggerLiftSafeAnimationAction(robot, trigger, 1, true, 0, 60.0, false)
        }
        lock (_gate) _state = 3;
        _needs.Add(NeedsActionSayName);                                                  // 0x0054C616..0x0054C61E, on the child's completion
        return Final();
    }

    /// <summary><c>CreateFineTuneAction</c> 0x0054C254..0x0054C3F4.</summary>
    private void CreateFineTuneAction()
    {
        lock (_gate) _state = 2;
        if (VerifiedFaceId is not { } id) { _child = null; return; }                     // +0x18C invalid: SetAction(null), state 2
        if (_v.Faces.GetFace(id) is not { } face)
        {
            Note("TurnTowardsFaceAction.FindTune.NullFace");
            _child = null;
            return;
        }
        ObservedFace = true;
        Note($"TurnTowardsFaceAction.CreateFinalAction.SawFace: Observed ID={id}. Will fine tune.");
        _needs.Add(NeedsActionSeeFace);                                                  // RegisterNeedsActionCompleted(SeeFace 0x2E)
        EmotionEvent?.Invoke("LookAtFaceVerified");
        float maxTurn = Math.Min(MathF.Abs((float)MaxTurnAngleRad), (float)FineTuneMaxTurnRad);         // Radians(min([+0x170], 0.7853982))
        _child = new TurnTowardsPoseAction(_v, face.HeadPose, maxTurn);
    }

    /// <summary>The final step 0x0054C622: <c>SetTurnedTowardsFace(+0x18C, true)</c> when the verified id is valid; success.</summary>
    private FaceActionResult Final()
    {
        if (VerifiedFaceId is { } id) _v.Faces.SetTurnedTowardsFace(id, true);
        return FaceActionResult.Success;
    }
}

/// <summary>
/// The engine's <c>TurnTowardsLastFacePoseAction</c> (M13-014): not a class of its own logic. The constructor builds a
/// <see cref="TurnTowardsFaceAction"/> with face id 0 - the invalid <c>SmartFaceID</c>, so <c>Init</c> takes the last observed face - and
/// overwrites its vtable with the one at 0x010204F4 (0x0055B3C0..0x0055B3CC, also 0x005DE160..0x005DE16C and 0x005B7DB2..0x005B7DC2),
/// whose Init and CheckIfDone slots relocate to <c>TurnTowardsFaceAction</c>'s; only the typeinfo word and the deleting destructor differ
/// (0x0052B099). So the type exists to be a distinct type and adds nothing else.
/// </summary>
// fidelity: M13-014
public sealed class TurnTowardsLastFacePoseAction : TurnTowardsFaceAction
{
    /// <summary>The face id the engine's constructor passes (0x0055B3BC <c>movs r2, #0</c>, M13-014).</summary>
    public const int EngineFaceIdArgument = 0;

    public TurnTowardsLastFacePoseAction(VisionSystem v, double maxTurnAngleRad, bool sayName)
        : base(v, EngineFaceIdArgument, maxTurnAngleRad, sayName) { }
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
// fidelity: M14-003
public sealed class TrackFaceAction : IDisposable
{
    // Fixture clock seam: production retains the existing UTC-based tracking duration.
    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    internal event Action? UpdateWaitObserved;

    public const double NeckHeightMm = 49.0;

    /// <summary>
    /// The pan and tilt tolerances an <c>ITrackAction</c> starts with, 0.0349066 rad (2 degrees), written
    /// into the action at +0x84 and +0x8C by its constructor (0x005646BC and 0x005646CC).
    /// </summary>
    public const uint MinToleranceBits = 0x3D0EFA35;   // movw/movt 0x005646BC..0x005646D4, Radians(float) at 0x005646C8/0x005646D8
    public static readonly double MinToleranceRad = BitConverter.Int32BitsToSingle(unchecked((int)MinToleranceBits));

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

    /// <summary>The cap on the body turn speed, 0x40A78D36 (the literal at 0x00565676), and the body turn's acceleration 0x436DFFDB (0x005656A8/0x005656AC).</summary>
    public const uint BodySpeedCapBits = 0x40A78D36, BodyTurnAccelBits = 0x436DFFDB;

    /// <summary>The body speed: <c>min(|pan| / 0.4f, 0x40A78D36)</c> in binary32 (0x00565672..0x00565688; NaN keeps the cap).</summary>
    // fidelity: M14-003
    internal static float PanSpeed(double pan)
    {
        float panSpeed = (float)Math.Abs(pan) / (float)PanDurationSec;
        float cap = BitConverter.Int32BitsToSingle(unchecked((int)BodySpeedCapBits));
        return panSpeed < cap ? panSpeed : cap;
    }

    /// <summary>
    /// How high the head may go while tracking, 0.776672 rad, at +0x94 (0x005646DC).
    /// </summary>
    public const uint MaxHeadAngleBits = 0x3F46D3F2;   // movw/movt 0x005646DC..0x005646E8 into +0x94
    public static readonly double MaxHeadAngleRad = BitConverter.Int32BitsToSingle(unchecked((int)MaxHeadAngleBits));

    /// <summary>
    /// The turn a sound needs before it plays, 0.174533 rad (10 degrees) for both axes, at +0xC0 and
    /// +0xC8 (0x00564722 and 0x00564732). No sound is set by default.
    /// </summary>
    public const uint MinAngleForSoundBits = 0x3E32B8C2;   // movw/movt 0x00564722..0x0056472A (+0xC0) and 0x00564732..0x0056473A (+0xC8)
    public static readonly double MinAngleForSoundRad = BitConverter.Int32BitsToSingle(unchecked((int)MinAngleForSoundBits));

    /// <summary>
    /// The time the action aims to reach the target in, 0.5 s, at +0xD8 (0x0056475C);
    /// <c>SetDesiredTimeToReachTarget</c> 0x00564AE4 writes it.
    /// </summary>
    public const double DesiredTimeToReachTargetSec = 0.5;

    /// <summary>
    /// The action tick. The engine's tracking runs inside <c>CheckIfDone</c>, which the action list calls
    /// every basestation tick; there is no update period of its own (its update timeout at +0x7C is not
    /// one, and the constructor leaves the three times at +0xE4..+0xEC at -1). The tick is 60 ms
    /// (0x03938700 ns): CozmoInstanceRunner::Run 0x0065B3D2/0x0065B3D8 -> CozmoEngine::Update 0x0065BB16 ->
    /// RobotManager::UpdateAllRobots 0x004ED648 -> Robot::Update 0x0052F6E4 -> ActionList::Update
    /// 0x005140BC -> ActionQueue::Update 0x0053F598 -> IActionRunner::Update 0x0053F628 ->
    /// IAction::UpdateInternal 0x00540592 -> ITrackAction::CheckIfDone (vtable slot 0x01022F1C). 33 ms is
    /// the M5 face keep-alive cadence, not an action tick.
    /// </summary>
    public const int UpdateIntervalMs = 60;
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
        // MISSING: the engine runs tracking as ITrackAction::CheckIfDone (0x00564F09..0x0056584B, 2372 bytes) once per ActionList tick (Robot::Update -> ActionList::Update 0x005140BC ->
        // IAction::UpdateInternal), after ITrackAction::Init (0x00564D35). The ActionList and ActionWatcher are built, but TrackFaceAction is not routed through them: CozmoMotion's UpdateActions is the only per-tick action hook (Engine.ActionRunnerUpdate) and
        // runs head/lift moves only. The wall-clock loop below is therefore NOT the engine's mechanism, and CheckIfDone's other branches are unread and unbuilt: the stop criteria
        // (StopCriteriaMetAndTimeToStop 0x0056594D), the small-angle clamping with its random periods (UpdateSmallAngleClamping 0x0056584D), the sound with its spacing, the eye shift
        // (+0xA1, 0x0056529C..0x0056535A), the update timeout, the mode, the driving-animation end and the 0x5654xx result mapping.
        SteppedBehavior.ReportMissing("TrackFaceAction (M14-003): ITrackAction::CheckIfDone 0x00564F09 on the ActionList tick is not built (TrackFaceAction is not routed through the built ActionList); the Task.Delay(60) loop is a stand-in and its stop criteria, small-angle clamping, sound, eye shift and timeout are unread");
        var end = UtcNow() + duration;
        while (UtcNow() < end && !cancel.IsCancellationRequested)
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
                // Head: 0x005650F2 vdiv.f32 |delta| / [+0xD0] straight into MoveHeadToAngle (PLT 0x4AB188, 0x00565104), no clamp (the 0.01 floor this stack had is removed).
                double headSpeed = (float)Math.Abs(targetHead - headNow) / (float)TiltDurationSec;
                // Body: 0x00565672..0x00565688 vdiv.f32 |pan| / [+0xD4], then the SMALLER of that and the literal 0x40A78D36 at 0x00565676 (vcmpe / it mi; a NaN keeps the cap);
                // MovementComponent::TurnInPlace (PLT 0x4AB020, 0x005656B4) with accel 0x436DFFDB (movw/movt 0x005656A8/0x005656AC), tolerance [+0x84] (0x00565696),
                // numHalfRevolutions 0 and isAbsolute 1.
                double bodySpeed = PanSpeed(pan);
                await PanAndTilt.RunAsync(_v, LastCommand.Value.Pan, LastCommand.Value.Tilt, bodySpeed, cancel,
                                          headSpeed, TrackAccelRadPerSec2, waitForSettle: false,
                                          bodyAccelRadPerSec2: BitConverter.Int32BitsToSingle(unchecked((int)BodyTurnAccelBits)), bodyToleranceRad: (float)PanToleranceRad);
            }
            UpdateWaitObserved?.Invoke();
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
