using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// The base of the behaviours that run async actions from a <see cref="SteppedBehavior"/>: an action's
/// completion is posted onto the behaviour's tick, the way the engine's <c>StartActing(action, callback)</c>
/// completes on the next update. Shared by the manipulation (M12/M13) and face (M14) behaviours.
/// </summary>
public abstract class ActionBehavior : SteppedBehavior
{
    private CancellationTokenSource? _cancel;

    protected ActionBehavior(string id, string behaviorClass) : base(id, behaviorClass) { }

    protected override bool KeepsRunningWithoutAction => _cancel is not null;

    protected void RunAction<T>(string what, Func<CancellationToken, Task<T>> action, Action<T> onDone, T failed)
    {
        _cancel?.Cancel();
        var cts = _cancel = new CancellationTokenSource();
        Log($"start {what}");
        ObserveAsyncWork(RunAsyncAction(() => action(cts.Token)).ContinueWith(t =>
        {
            // Stack-owned diagnostics survive a stopped/replaced action; onDone still uses Post and its stale guard.
            if (t.IsFaulted) Log($"{what} faulted: {t.Exception}"); // AggregateException.ToString retains every inner and stack.
            else if (t.IsCanceled)
            {
                try { t.GetAwaiter().GetResult(); }
                catch (OperationCanceledException error)
                {
                    Log($"{what} cancelled (observation stack from completed task; original cancellation throw site may be unavailable): {error}");
                }
            }
            var r = t.Status == TaskStatus.RanToCompletion ? t.Result : failed;
            Post(() =>
            {
                if (_cancel != cts) return;          // stopped meanwhile
                _cancel = null;
                Log($"{what} -> {r}");
                onDone(r);
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
    }

    protected void CancelAction() { _cancel?.Cancel(); _cancel = null; }

    /// <summary>Fires an emotion event on the attached mood, logging whether the loaded model knows it.</summary>
    protected void EmotionEvent(string name)
    {
        double nowSec = Clock() / 1000.0;
        bool known = Context.Mood?.Trigger(name, nowSec) ?? false;
        Log($"emotion event {name}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
    }

    protected override void OnStop(BehaviorStopReason reason) => CancelAction();
}

/// <summary>
/// Shared plumbing for the face behaviours (M14): the vision system's face world and the turn-to-face action.
/// None of these is runnable without a face in the world, and faces arrive only from an attached
/// <see cref="IFaceDetector"/>; the stock OKAO one is unavailable, which the inventory records.
/// </summary>
public abstract class FaceBehavior : ActionBehavior
{
    protected readonly VisionSystem V;
    protected FaceBehavior(string id, string behaviorClass, VisionSystem v) : base(id, behaviorClass) => V = v;

    protected void RunFaceAction(string what, Func<CancellationToken, Task<FaceActionResult>> action, Action<FaceActionResult> onDone) =>
        RunAction(what, action, onDone, FaceActionResult.Abort);

    /// <summary>Plays the turn's say-name / no-name reaction (the engine's <c>SayTextAction</c> with a trigger, or the lift-safe trigger), then continues.</summary>
    protected void PlayReaction((AnimationTrigger? Trigger, string? NameToSay)? reaction, Action onDone)
    {
        if (reaction is null) { onDone(); return; }
        var (trigger, name) = reaction.Value;
        if (name is not null) Log($"SayTextAction(\"{name}\") (text-to-speech is the app's; DEFERRED)");
        if (trigger is { } t) PlayTrigger(t, onDone); else onDone();
    }

    protected override void OnStop(BehaviorStopReason reason) => base.OnStop(reason);
}

/// <summary>
/// <c>BehaviorPlayAnimSequenceWithFace</c> (0x005C0648): <c>InitInternal</c> runs a
/// <c>TurnTowardsFaceAction(robot, lastFace (−1), π, sayName=false)</c> and then plays the config's
/// <c>animTriggers</c> in sequence (the <c>BehaviorPlayAnimSequence</c> base). Runnable when a face is known.
/// Seven shipped configs (FeedingPlayRequestAtFace ×2, VC_AlrightyResponse, VC_HowAreYouDoing ×4).
/// </summary>
public sealed class PlayAnimWithFaceBehavior : FaceBehavior
{
    public PlayAnimWithFaceBehavior(VisionSystem v, string id, IReadOnlyList<AnimationTrigger> triggers) : base(id, "PlayAnimWithFace", v) => Triggers = triggers;
    public IReadOnlyList<AnimationTrigger> Triggers { get; }
    public bool TurnedToFace { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => V.Faces.HasAnyFaces();

    protected override void OnStart()
    {
        using var turn = new TurnTowardsFaceAction(V, SmartFaceID.Invalid, Math.PI, sayName: false);
        RunFaceAction("TurnTowardsFace(last face)", turn.RunAsync, r =>
        {
            foreach (var l in turn.Trace) Log("  " + l);
            TurnedToFace = r == FaceActionResult.Success;
            int i = 0;
            void Next() { if (i >= Triggers.Count) { Finish(); return; } PlayTrigger(Triggers[i++], Next); }
            Next();
        });
    }
}

/// <summary>
/// <c>IReactToFaceListener</c> as <c>BehaviorAcknowledgeFace</c> calls it (inventory ACKNOWLEDGE-FACE-1, research/20261029-acknowledge-face-1-read.md):
/// slot 0 <c>FinishedReactingToFace(robot, id)</c> from <c>FinishIteration</c> (F1, 0x00602C34..0x00602C96) and slot 1 <c>ClearDesiredTargets</c> from
/// <c>StopInternal</c> (S1, 0x006028D2..0x00602926). The Robot argument is not modelled.
/// </summary>
public interface IReactToFaceListener
{
    void FinishedReactingToFace(int faceId);
    void ClearDesiredTargets();
}

/// <summary>
/// <c>AIWhiteboard::GetBestFaceToTrack(targets, bool)</c> 0x0056C110..0x0056C27E (inventory ACKNOWLEDGE-FACE-1 B2; inputs read by the manager
/// 2026-10-29, with <c>TurnTowardsPoseAction::GetRelativeBodyAngleToLookAtPose</c> 0x0054B584..0x0054B59A). An empty set gives 0; a single
/// element is returned unchecked; otherwise the set is walked in ascending id order from best = FLT_MAX (0x7F7FFFFF, literal 0x0056C158) and
/// result 0: a face <c>FaceWorld::GetFace</c> does not have is skipped, as is one whose pose (face+0xF4) cannot be taken with respect to the
/// robot (0x0056C1AC..0x0056C1BC); the cost (0x0056C1E4..0x0056C22E, all f32) replaces the best on a strict less-than (vcmpe/mi), so a tie keeps
/// the earlier, lower id. With every face skipped the result stays 0.
/// </summary>
internal static class BestFaceToTrack
{
    /// <summary>The weight on the body angle in B2's cost.</summary>
    public const float BodyAngleWeight = 3.0f;
    /// <summary>s18 when the bool argument is true: the surcharge on a face with an empty name (face+0x14).</summary>
    public const float UnnamedSurcharge = 1000.0f;
    /// <summary>The starting best, FLT_MAX (0x7F7FFFFF, 0x0056C158).</summary>
    public static readonly float InitialBest = BitConverter.Int32BitsToSingle(0x7F7FFFFF);

    /// <summary>
    /// The f32 cost of one face, given its translation relative to the robot (0x0056C1D0..0x0056C22E):
    /// head = GetAbsoluteHeadAngleToLookAtPose(p) (M13-026, 0x0054B428); body = Radians(atan2f(p.y, p.x)) (0x0054B588..0x0054B596; atan2f is
    /// bionic libm, MathF.Atan2 stands in under the existing libm EQUIVALENT policy); headTerm = |Radians(head) - robot+0x2FC| with Radians
    /// operator- (0x4A5A7C, normalising); bodyTerm = |body| * 3.0f; cost = (headTerm + bodyTerm) + surcharge, the surcharge being 0 for a named
    /// face and s18 otherwise (1000.0f only when <paramref name="surchargeUnnamed"/>, else 0.0f).
    /// </summary>
    public static float Cost(Vec3 relative, float robotHeadAngle, bool hasName, bool surchargeUnnamed)
    {
        float head = (float)TurnTowardsPoseCompound.AbsoluteHeadAngleToLookAtPose(relative);
        var body = new Radians(MathF.Atan2((float)relative.Y, (float)relative.X));
        float headTerm = MathF.Abs((new Radians(head) - new Radians(robotHeadAngle)).Value);
        float bodyTerm = MathF.Abs(body.Value) * BodyAngleWeight;
        float s18 = surchargeUnnamed ? UnnamedSurcharge : 0.0f;
        float surcharge = hasName ? 0.0f : s18;
        return (headTerm + bodyTerm) + surcharge;
    }

    public static int Get(IReadOnlyCollection<int> targets, FaceWorld faces, Pose3d? robotPose, float robotHeadAngle, bool surchargeUnnamed)
    {
        if (targets.Count == 0) return 0;                                          // B2: empty -> 0
        if (targets.Count == 1) return targets.First();                            // B2: one element, returned unchecked
        int best = 0;
        float bestCost = InitialBest;
        foreach (int id in targets.OrderBy(i => i))                                // B2: ascending walk of the std::set<int>
        {
            if (faces.GetFace(id) is not { } face) continue;                       // GetFace null -> skip
            if (robotPose is null) continue;                                       // no robot pose: GetWithRespectTo fails -> skip
            Pose3d relative;
            // The stack's face poses are the flat legacy poses the face actions already take relative to the robot this way (M11-066 labelled
            // adapter inside WithRespectTo); unrelated roots are the engine's GetWithRespectTo false -> skip.
            try { relative = face.HeadPose.WithRespectTo(robotPose); }
            catch (InvalidOperationException) { continue; }
            float cost = Cost(relative.Translation, robotHeadAngle, face.HasName, surchargeUnnamed);
            if (cost < bestCost) { best = id; bestCost = cost; }                   // strict <: a tie keeps the lower id
        }
        return best;
    }
}

/// <summary>
/// <c>BehaviorAcknowledgeFace</c> (factory class 0x3A, ctor 0x00602884; inventory ACKNOWLEDGE-FACE-1, research/20261029-acknowledge-face-1-read.md).
/// <list type="bullet">
/// <item>C2: the target set (+0x120), the listener set (+0x130), the begin-pending flag (+0x12C) and the greeting-played flag (+0x12D). No JSON key
/// is read (C3).</item>
/// <item>R1: the vtable+0x20/+0x24 gates are the base's false (0x005BF04C, 0x0059EC12) and the class's +0x28 override is false (0x00602D20): not
/// runnable while carrying. R2: runnable iff the target set is non-empty (0x006028BC..0x006028C6).</item>
/// <item>I1/U1: Init sets the begin-pending flag and starts nothing (0x006028C8..0x006028D0); the first Update clears it and calls
/// <c>BeginIteration</c> (0x00602928..0x0060294A).</item>
/// <item>BeginIteration 0x00602950..0x00602AA6: B1 GetBestFaceToTrack(set, false), 0 starts nothing; B3 <c>TurnTowardsFaceAction(id, pi
/// 0x40490FDB, sayName 1)</c>; B4 the greeting gate and its info log; B6 the greeting say-name callback or B7 the say-name trigger
/// AcknowledgeFaceNamed; B8 the no-name trigger AcknowledgeFaceUnnamed; B9 3 images to wait (+0x188); B10 StartActing with FinishIteration on
/// any result.</item>
/// <item>F1 FinishIteration 0x00602C34..0x00602C96: erase the id, notify the listeners, BehaviorObjectiveAchieved(ReactedAcknowledgedFace 0x1A,
/// true), BeginIteration again.</item>
/// <item>S1 StopInternal 0x006028D2..0x00602926: the listeners' ClearDesiredTargets, the set cleared, the greeting flag kept.</item>
/// </list>
/// </summary>
// fidelity: M7-018
public sealed class AcknowledgeFaceBehavior : FaceBehavior
{
    /// <summary>B4: the greeting window, 60.0f (0x42700000).</summary>
    public static readonly float GreetingWindowSec = BitConverter.Int32BitsToSingle(0x42700000);
    /// <summary>B3: <c>Radians</c> pi, 0x40490FDB.</summary>
    public static readonly float MaxTurnAngleRad = BitConverter.Int32BitsToSingle(0x40490FDB);
    /// <summary>B9: action+0x188 = 3 (0x00602A94..0x00602A98).</summary>
    public const int ImagesToWait = 3;
    /// <summary>F1: BehaviorObjective ReactedAcknowledgedFace.</summary>
    public const uint ObjectiveReactedAcknowledgedFace = 0x1A;
    /// <summary>B6: the mood event the greeting callback fires (face_events.json:40-46, M7-020).</summary>
    public const string GreetingEmotionEvent = "GreetingSayName";

    private readonly object _setGate = new();
    private readonly SortedSet<int> _targets = new();                    // +0x120 std::set<int>
    private readonly List<IReactToFaceListener> _listeners = new();      // +0x130 std::set<IReactToFaceListener*>
    private bool _beginPending;                                          // +0x12C
    private volatile bool _greetingPlayed;                               // +0x12D, never reset (S1)
    private int _currentTarget;                                          // +0x11C (ctor 0)
    private TurnTowardsFaceAction? _turn;

    public AcknowledgeFaceBehavior(VisionSystem v, string id = "AcknowledgeFace") : base(id, "AcknowledgeFace", v) { }

    /// <summary>The face the current (or last) iteration targets; null before the first.</summary>
    public int? TargetFaceId { get; private set; }
    /// <summary>+0x12D: the greeting callback has run.</summary>
    public bool GreetingPlayed => _greetingPlayed;
    /// <summary>The turn the last iteration built, for inspection.</summary>
    internal TurnTowardsFaceAction? LastTurn { get; private set; }
    /// <summary>The manager whose +0x54 B4 reads; null: the robot's native BehaviorManager.</summary>
    internal BehaviorManager? Manager { get; set; }

    /// <summary>The target set +0x120.</summary>
    public IReadOnlyCollection<int> Targets { get { lock (_setGate) return _targets.ToList(); } }

    /// <summary>T4: the strategy copies its desiredFaces into +0x120 (0x0060CAB0..0x0060CB5E).</summary>
    public void SetTargets(IEnumerable<int> faces)
    {
        lock (_setGate) { _targets.Clear(); foreach (int f in faces) _targets.Add(f); }
    }

    /// <summary>T3: vslot AP+0x34 AddListener (reloc 0x0102B870), called by the strategy (0x0060D074..0x0060D080).</summary>
    public void AddListener(IReactToFaceListener listener) { lock (_setGate) if (!_listeners.Contains(listener)) _listeners.Add(listener); }
    /// <summary>Host teardown only (the strategy's Dispose); the engine's destructor order is not in the inventory.</summary>
    public void RemoveListener(IReactToFaceListener listener) { lock (_setGate) _listeners.Remove(listener); }
    private IReactToFaceListener[] Listeners() { lock (_setGate) return _listeners.ToArray(); }

    // fidelity: M8-001 -- R1: the base's +0x20/+0x24 slots return 0 (0x005BF04C, 0x0059EC12); the class's +0x28 slot returns 0 (0x00602D20, vtable 0x0102B834).
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;

    /// <summary>R2: +0x128 (the set's size) != 0.</summary>
    protected override bool IsRunnableInternal(BehaviorContext context) { lock (_setGate) return _targets.Count != 0; }

    protected override bool KeepsRunningWithoutAction => _beginPending || base.KeepsRunningWithoutAction;

    /// <summary>I1: +0x12C = 1, return 0; no action is started.</summary>
    protected override void OnStart() => _beginPending = true;

    /// <summary>U1: if +0x12C, clear it and BeginIteration; then the base UpdateInternal.</summary>
    protected override void OnUpdate()
    {
        if (!_beginPending) return;
        _beginPending = false;
        BeginIteration();
    }

    private float ManagerGreetingTime()
    {
        if ((Manager ?? Context.Robot.BehaviorManager) is { } manager) return manager.GreetingTimeField54;
        // No manager owner: every manager the stack builds holds the ctor value at +0x54, since no writer is known (B5).
        ReportMissing("ACKNOWLEDGE-FACE-1 B4: no BehaviorManager owner to read +0x54 from; the ctor value -1.0f (0x005A08E4..0x005A08F0) is read");
        return BehaviorManager.GreetingTimeField54Initial;
    }

    private void BeginIteration()
    {
        int[] targets;
        lock (_setGate) targets = _targets.ToArray();
        // B1: GetBestFaceToTrack(set, false); the robot pose is the one the face actions use (TurnTowardsFaceAction.Init), robot+0x2FC is Motion's.
        int id = BestFaceToTrack.Get(targets, V.Faces, V.History.Latest?.RobotPose, Context.Robot.Motion.HeadAngleRad, surchargeUnnamed: false);
        if (id == 0) return;                                                       // B1: 0 -> no action
        _currentTarget = id;
        TargetFaceId = id;
        var turn = new TurnTowardsFaceAction(V, id, MaxTurnAngleRad, sayName: true);   // B3: sayName always 1
        // B4: (now - [mgr+0x54] <= 60.0) && [mgr+0x54] >= 0 && !HasTurnedTowardsFace && !+0x12D
        float now = (float)(Context.ClockSec?.Invoke() ?? Clock() / 1000.0);
        float t = ManagerGreetingTime();
        bool alreadyTurned = V.Faces.HasTurnedTowardsFace(id);
        bool greet = now - t <= GreetingWindowSec && t >= 0f && !alreadyTurned && !_greetingPlayed;
        string text = $"currTime = {now:F6}, alreadyTurned:{(alreadyTurned ? 1 : 0)}, shouldPlayGreeting:{(greet ? 1 : 0)}";
        Log("AcknowledgeFace.DoAcknowledgement: " + text);
        Context.Robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Behaviors", "AcknowledgeFace.DoAcknowledgement", text);
        if (greet)
        {
            // B6: SetSayNameTriggerCallback, lambda 0x00602D8C..0x00602DDE: +0x12D = 1, GreetingSayName, return 301 NamedFaceInitialGreeting.
            turn.SetSayNameTriggerCallback(_ =>
            {
                _greetingPlayed = true;
                EmotionEvent(GreetingEmotionEvent);
                return AnimationTrigger.NamedFaceInitialGreeting;
            });
        }
        else turn.SayNameTrigger = AnimationTrigger.AcknowledgeFaceNamed;          // B7 (0x00602A7C..0x00602A80)
        turn.NoNameTrigger = AnimationTrigger.AcknowledgeFaceUnnamed;              // B8 (0x00602A8C..0x00602A90)
        turn.ImagesToWait = ImagesToWait;                                          // B9
        turn.EmotionEvent += EmotionEvent;
        _turn = turn; LastTurn = turn;
        // B10: StartActing with FinishIteration, run on any result (the result is ignored).
        RunFaceAction($"TurnTowardsFace({id})", turn.RunAsync, r =>
        {
            foreach (var l in turn.Trace) Log("  " + l);
            PlayReaction(turn.Reaction, () =>
            {
                if (ReferenceEquals(_turn, turn)) _turn = null;
                turn.Dispose();
                FinishIteration();
            });
        });
    }

    /// <summary>F1: erase the id, listeners' FinishedReactingToFace, BehaviorObjectiveAchieved(0x1A, true), BeginIteration.</summary>
    private void FinishIteration()
    {
        int id = _currentTarget;
        lock (_setGate) _targets.Remove(id);
        foreach (var l in Listeners()) l.FinishedReactingToFace(id);
        Context.Robot.Engine.ReportBehaviorObjective(ObjectiveReactedAcknowledgedFace, "ReactedAcknowledgedFace", Id);
        Log($"BehaviorObjectiveAchieved(0x{ObjectiveReactedAcknowledgedFace:X}, true)");
        BeginIteration();
    }

    /// <summary>S1: the listeners' ClearDesiredTargets (slot 1, 0x0060D0CA) and the set cleared; +0x12D is kept.</summary>
    protected override void OnStop(BehaviorStopReason reason)
    {
        foreach (var l in Listeners()) l.ClearDesiredTargets();
        lock (_setGate) _targets.Clear();
        base.OnStop(reason);
        _turn?.Dispose(); _turn = null;
    }
}

/// <summary>
/// <c>BehaviorInteractWithFaces</c> (0x005C1F00..0x005C2A60; params <c>minTimeToTrackFace_s</c> 8,
/// <c>maxTimeToTrackFace_s</c> 15, <c>clampSmallAngles</c>, <c>minClampPeriod_s</c> 0.2, <c>maxClampPeriod_s</c> 0.7):
/// runnable when <c>SelectFaceToTrack</c> finds a face observed recently (<c>GetFaceIDsObservedSince</c>).
/// <c>TransitionToInitialReaction</c> ("VerifyFace"): <c>TurnTowardsFaceAction(id, π, sayName)</c> with
/// 0xF8 <see cref="AnimationTrigger.InteractWithFacesInitialNamed"/> / 0xF9 <see cref="AnimationTrigger.InteractWithFacesInitialUnnamed"/>;
/// <c>TransitionToGlancingDown</c> → <c>TransitionToDrivingForward</c>: when the memory map allows
/// (<c>CanDriveIdealDistanceForward</c>) a <c>DriveStraightAction(40 mm)</c> (else −15 mm) in parallel with a
/// <c>TrackFaceAction</c> (tolerances 4°, 0.0698132) that stops when the drive completes;
/// <c>TransitionToTrackingFace</c>: track for a random time in [min, max] ("will track for %f seconds") with
/// 0xF7 <see cref="AnimationTrigger.InteractWithFaceTrackingIdle"/>; then <c>TransitionToTriggerEmotionEvent</c>:
/// "InteractWithNamedFace" / "InteractWithUnnamedFace", objective <c>InteractedWithFace</c>. The drive needs the
/// manipulation system; without one the glance and drive are skipped (logged).
/// </summary>
public sealed class InteractWithFacesBehavior : FaceBehavior
{
    public enum Phase { Idle, VerifyFace, GlancingDown, DrivingForward, TrackingFace, TriggerEmotionEvent }
    public const double TrackToleranceRad = 0.0698132;
    public const double DriveForwardMm = 40.0;
    /// <summary>
    /// What it drives instead when the memory map says the way is not clear: -15 mm
    /// (<c>vmov.f32 s0, #-15.0</c> at 0x005C2566, replaced by 40 only when
    /// <see cref="CanDriveIdealDistanceForward"/> is true, 0x005C2572).
    /// </summary>
    public const double DriveBackwardMm = -15.0;
    /// <summary>Both drives run at 40 mm/s (0x42200000 at 0x005C2586).</summary>
    public const float DriveSpeedMmps = 40f;
    public const double DriveBackMm = -15.0;
    public const uint RecentFaceWindowMs = 10000;

    private readonly ManipulationSystem? _m;

    public InteractWithFacesBehavior(VisionSystem v, string id = "InteractWithFaces", ManipulationSystem? m = null, double minTrackSec = 8, double maxTrackSec = 15)
        : base(id, "InteractWithFaces", v) { _m = m; MinTrackSec = minTrackSec; MaxTrackSec = maxTrackSec; }

    public double MinTrackSec { get; }
    public double MaxTrackSec { get; }
    /// <summary>Test hook: scales the tracking time (the engine tracks 8–15 s).</summary>
    public double TrackTimeScale { get; set; } = 1.0;
    public Phase CurrentPhase { get; private set; }
    public int? TargetFaceId { get; private set; }
    public double? TrackSeconds { get; private set; }
    public TrackFaceAction? Tracker { get; private set; }

    /// <summary>The tracker holds a smart face id subscribed to the face world; stopping releases it.</summary>
    protected override void OnStop(BehaviorStopReason reason)
    {
        Tracker?.Dispose();
        Tracker = null;
        base.OnStop(reason);
    }

    private int? SelectFaceToTrack()
    {
        uint now = V.History.Latest?.Timestamp ?? 0;
        var ids = V.Faces.GetFaceIDsObservedSince(now > RecentFaceWindowMs ? now - RecentFaceWindowMs : 0);
        return ids.Count == 0 ? null : ids.Select(V.Faces.GetFace).Where(f => f is not null).OrderByDescending(f => f!.LastObservedTimestamp).First()!.Id;
    }

    protected override bool IsRunnableInternal(BehaviorContext context) => SelectFaceToTrack() is not null;

    protected override void OnStart()
    {
        TargetFaceId = SelectFaceToTrack();
        if (TargetFaceId is null) { Log("BehaviorInteractWithFaces.Init.NoValidTarget"); Finish(); return; }
        CurrentPhase = Phase.VerifyFace;
        using var turn = new TurnTowardsFaceAction(V, TargetFaceId.Value, Math.PI, sayName: true) { RequireVerifiedFace = true /* fidelity: M13-014, 0x005C229C stores the ctor's sayName bool at +0x193 */, SayNameTrigger = AnimationTrigger.InteractWithFacesInitialNamed, NoNameTrigger = AnimationTrigger.InteractWithFacesInitialUnnamed };
        turn.EmotionEvent += EmotionEvent;
        RunFaceAction($"VerifyFace: TurnTowardsFace({TargetFaceId})", turn.RunAsync, r =>
        {
            foreach (var l in turn.Trace) Log("  " + l);
            if (r != FaceActionResult.Success) { Log($"initial reaction failed: {r}"); Finish(); return; }
            PlayReaction(turn.Reaction, TransitionToGlancingDown);
        });
    }

    /// <summary>
    /// <c>BehaviorInteractWithFaces::CanDriveIdealDistanceForward</c> 0x005C2420: it takes the point
    /// <see cref="DriveForwardMm"/> ahead of the robot - the local (40, 0, 0) at 0x005C2442 turned by the
    /// robot's rotation and added to its translation - and asks the memory map
    /// <c>HasCollisionRayWithTypes(robot, thatPoint, types)</c> (the virtual at map+0x30, 0x005C24A2),
    /// returning the negation. The type mask is the array at 0x00C67962, which is
    /// <see cref="Vision.MemoryMapTypes.BlocksTheRobot"/>.
    ///
    /// With no map attached there is nothing to ask, and the engine's own answer in that case is the one
    /// its map gives for ground it knows nothing about: no collision, so the drive goes ahead.
    /// </summary>
    public bool CanDriveIdealDistanceForward()
    {
        if (Context.Map is not { } map || _m?.RobotPose() is not { } pose) return true;
        var from = pose.Translation;
        var to = pose.Apply(new Vision.Vec3(DriveForwardMm, 0, 0));
        return !map.HasCollisionRayWithTypes(new Vision.Vec2(from.X, from.Y), new Vision.Vec2(to.X, to.Y));
    }

    private void TransitionToGlancingDown()
    {
        CurrentPhase = Phase.GlancingDown;
        if (_m is null) { Log("no manipulation system: skipping the glance and the drive forward"); TransitionToTrackingFace(); return; }
        CurrentPhase = Phase.DrivingForward;
        // TransitionToDrivingForward 0x005C254E: the ideal 40 mm when the map says the way is clear, -15 mm
        // when it does not, both at 40 mm/s
        double distance = CanDriveIdealDistanceForward() ? DriveForwardMm : DriveBackwardMm;
        if (distance < 0) Log("the memory map has something in the way: backing off instead of driving in");
        Tracker?.Dispose();
        Tracker = new TrackFaceAction(V, TargetFaceId!.Value) { PanToleranceRad = TrackToleranceRad, TiltToleranceRad = TrackToleranceRad };
        RunAction($"DriveStraight({distance} mm) with TrackFace", async ct =>
        {
            using var trackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var track = Tracker.RunAsync(TimeSpan.FromSeconds(30), trackCts.Token);
            var r = await new DriveStraightAction(_m, distance, DriveSpeedMmps).RunAsync(ct);
            trackCts.Cancel();
            await track;
            return r;
        }, _ => TransitionToTrackingFace(), ActionResult.Abort);
    }

    private void TransitionToTrackingFace()
    {
        CurrentPhase = Phase.TrackingFace;
        TrackSeconds = (MinTrackSec + Context.Random.NextDouble() * (MaxTrackSec - MinTrackSec)) * TrackTimeScale;
        Log($"BehaviorInteractWithFaces.TrackTime: will track for {TrackSeconds / TrackTimeScale:F1} seconds");
        Tracker?.Dispose();
        Tracker = new TrackFaceAction(V, TargetFaceId!.Value) { PanToleranceRad = TrackToleranceRad, TiltToleranceRad = TrackToleranceRad };
        PlayTrigger(AnimationTrigger.InteractWithFaceTrackingIdle, () => { });
        RunAction("TrackFaceAction", ct => Tracker.RunAsync(TimeSpan.FromSeconds(TrackSeconds.Value), ct), _ => TransitionToTriggerEmotionEvent(), false);
    }

    private void TransitionToTriggerEmotionEvent()
    {
        CurrentPhase = Phase.TriggerEmotionEvent;
        var face = V.Faces.GetFace(TargetFaceId!.Value);
        EmotionEvent(face is { HasName: true } ? "InteractWithNamedFace" : "InteractWithUnnamedFace");
        Log("objective achieved: InteractedWithFace");
        CurrentPhase = Phase.Idle;
        Finish();
    }
}

/// <summary>
/// <c>BehaviorDriveToFace</c> (0x005DA550..0x005DAE00; <c>VC_ComeHere</c>): runnable with the last observed
/// face in the current origin (or any face observed since the start). <c>TransitionToTurningTowardsFace</c>:
/// <c>TurnTowardsFaceAction(π)</c>, <c>VisuallyVerifyFaceAction</c>, <c>TurnTowardsFaceAction(π)</c>; then
/// <c>IsCozmoAlreadyCloseEnoughToFace</c> (distance ≤ 200 mm, 0x43480000) → <c>TransitionToAlreadyCloseEnough</c>,
/// else <c>TransitionToDrivingToFace</c>: <c>DriveStraightAction(distance − 200, 60 (0x42700000))</c> with
/// <c>SetDecel(166.667)</c>; then <c>TransitionToTrackingFace</c>: track for 5 s. <c>CalculateDistanceToFace</c>
/// is the planar distance from the robot to the face's head pose.
/// </summary>
public sealed class DriveToFaceBehavior : FaceBehavior
{
    public enum Phase { Idle, TurningTowardsFace, DrivingToFace, AlreadyCloseEnough, TrackingFace }
    public const double CloseEnoughMm = 200.0;
    public const float DriveSpeedMmps = 60f;
    public const float DriveDecelMmps2 = 166.667f;
    public const double TrackSec = 5.0;
    private readonly ManipulationSystem _m;

    public DriveToFaceBehavior(VisionSystem v, ManipulationSystem m, string id = "VC_ComeHere") : base(id, "DriveToFace", v) => _m = m;
    public Phase CurrentPhase { get; private set; }
    public int? TargetFaceId { get; private set; }
    public double? DistanceMm { get; private set; }
    public double TrackTimeScale { get; set; } = 1.0;

    protected override bool IsRunnableInternal(BehaviorContext context) => V.Faces.GetLastObservedFace() is not null;

    protected override void OnStart()
    {
        var face = V.Faces.GetLastObservedFace();
        if (face is null) { Log("BehaviorDriveToFace.InitInternal.NoValidFace"); Finish(); return; }
        TargetFaceId = face.Id;
        CurrentPhase = Phase.TurningTowardsFace;
        RunFaceAction($"TurnTowardsFace({face.Id}), verify, turn", async ct =>
        {
            using var t1 = new TurnTowardsFaceAction(V, face.Id); var r = await t1.RunAsync(ct); foreach (var l in t1.Trace) Log("  " + l);
            if (r != FaceActionResult.Success) return r;
            var v = await new VisuallyVerifyFaceAction(V, face.Id).RunAsync(ct);
            Log($"  VisuallyVerifyFace -> {v}");
            using var t2 = new TurnTowardsFaceAction(V, face.Id); return await t2.RunAsync(ct);
        }, r =>
        {
            if (r != FaceActionResult.Success) { Finish(); return; }
            var f = V.Faces.GetFace(face.Id); var robot = V.History.Latest?.RobotPose;
            if (f is null || robot is null) { Finish(); return; }
            var d = f.HeadPose.Translation - robot.Value.Translation;
            DistanceMm = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            if (DistanceMm <= CloseEnoughMm) { CurrentPhase = Phase.AlreadyCloseEnough; Log($"already close enough ({DistanceMm:F0} mm)"); TransitionToTrackingFace(); return; }
            CurrentPhase = Phase.DrivingToFace;
            RunAction($"DriveStraight({DistanceMm - CloseEnoughMm:F0} mm @ {DriveSpeedMmps})", ct => new DriveStraightAction(_m, DistanceMm.Value - CloseEnoughMm, DriveSpeedMmps).RunAsync(ct),
                      _ => TransitionToTrackingFace(), ActionResult.Abort);
        });
    }

    private void TransitionToTrackingFace()
    {
        CurrentPhase = Phase.TrackingFace;
        var tracker = new TrackFaceAction(V, TargetFaceId!.Value);
        RunAction("TrackFace 5 s", ct => tracker.RunAsync(TimeSpan.FromSeconds(TrackSec * TrackTimeScale), ct), _ => { CurrentPhase = Phase.Idle; Finish(); }, false);
    }
}

/// <summary>
/// <c>BehaviorSearchForFace</c> (0x005C9158..0x005C9400; <c>VC_SearchForFace</c>): <c>TransitionToSearchingAnimation</c>
/// plays 0x65 <see cref="AnimationTrigger.ComeHere_SearchForFace"/> (repeating while no face is seen);
/// <c>UpdateInternal</c> watches <c>FaceWorld::HasAnyFaces(since start)</c> and on a face stops acting and
/// <c>TransitionToFoundFace</c> plays 0x66 <see cref="AnimationTrigger.ComeHere_SearchForFace_FoundFace"/>.
/// Runnable always (it is the voice command's response); <see cref="MaxSearches"/> bounds the repeat (INFERRED).
/// </summary>
public sealed class SearchForFaceBehavior : FaceBehavior
{
    public enum Phase { Idle, Searching, FoundFace }
    public const int MaxSearches = 3;
    public SearchForFaceBehavior(VisionSystem v, string id = "VC_SearchForFace") : base(id, "SearchForFace", v) { }
    public Phase CurrentPhase { get; private set; }
    public int Searches { get; private set; }
    public bool Found { get; private set; }
    private uint _startTs;

    protected override void OnStart()
    {
        _startTs = V.History.Latest?.Timestamp ?? 0;
        Searches = 0; Found = false;
        TransitionToSearchingAnimation();
    }

    private void TransitionToSearchingAnimation()
    {
        CurrentPhase = Phase.Searching;
        Searches++;
        PlayTrigger(AnimationTrigger.ComeHere_SearchForFace, () =>
        {
            if (Found) return;
            if (V.Faces.HasAnyFaces(_startTs + 1)) { TransitionToFoundFace(); return; }
            if (Searches >= MaxSearches) { Log("no face found"); CurrentPhase = Phase.Idle; Finish(); return; }
            TransitionToSearchingAnimation();
        });
    }

    protected override void OnUpdate()
    {
        if (CurrentPhase == Phase.Searching && !Found && V.Faces.HasAnyFaces(_startTs + 1)) { StopActing(); TransitionToFoundFace(); }
    }

    private void TransitionToFoundFace()
    {
        Found = true;
        CurrentPhase = Phase.FoundFace;
        PlayTrigger(AnimationTrigger.ComeHere_SearchForFace_FoundFace, () => { CurrentPhase = Phase.Idle; Finish(); });
    }
}

/// <summary>
/// <c>BehaviorReactToPet</c> (0x00606ED8..0x00607560; reactions/reactToPet.json): runnable when
/// <c>PetWorld</c> knows a pet and no reaction is running. <c>BeginIteration</c>: the pet's rectangle centre
/// becomes a <c>TurnTowardsImagePointAction</c>, then <c>TriggerAnimationAction(GetAnimationTrigger(type))</c>
/// (<see cref="AnimationTrigger.PetDetectionCat"/> / <see cref="AnimationTrigger.PetDetectionDog"/>; one in
/// twenty picks <see cref="AnimationTrigger.PetDetectionSneeze"/>, INFERRED from <c>RandInt(20)</c> against 0x14)
/// and a <c>TrackPetFaceAction(type)</c> with an update timeout, for a random duration ("Reacting to petID %d
/// type %d from t=%f to t=%f"); <c>EndIteration</c> when the time is up. Tracking a pet's image point is
/// DEFERRED (no head pose for pets); the turn towards the image point is a head-and-body turn to the ray
/// through the rectangle centre.
/// </summary>
// fidelity: M10-003
/// <summary>
/// <c>ReactionTriggerStrategyFacePositionUpdated</c> (gap2 3a..3g, gap1 8); flags (1, 1, 0).
/// <list type="bullet">
/// <item>Ctor (3a): last-reaction time +0x54 = −1; desiredFaces +0x58; reactedFaces +0x64; the per-face "is close" map +0x70.</item>
/// <item>EnabledStateChanged, either value (3b): desiredFaces cleared.</item>
/// <item>AddDesiredFace (3d): inserts only while trigger 2 is enabled.</item>
/// <item>Face observed (3e, 0x60CB84..0x60CE20): return for an id &lt; 0 or a face FaceWorld does not have; a named face
/// not yet reacted to is added ("named for the first time") and the observation counts as handled; the distance from the
/// robot pose to the face pose (failure: error and return); threshold 400 mm for a face in the map that is close, 300 mm
/// otherwise; AddDesiredFace and "FaceBecomeClose" only when the face was known and not close, is now close, was not
/// handled as first-named, and there is no cooldown (last ≥ 0 &amp;&amp; now &lt; last + 4.0 s); the close map always takes
/// the new value.</item>
/// <item>STBI (0x60CAB0..0x60CB5E): desiredFaces empty → false; AcknowledgeFace+0x120 = the set; on the charger platform →
/// false; else IsRunnable. The base per-target map is never filled for faces (3g).</item>
/// <item>FinishedReactingToFace (3f): the base's record (none for faces, so a debug log), reactedFaces += id, last = now;
/// ClearDesiredTargets clears desiredFaces. RobotDelocalized's ResetReactionData changes nothing (3c).</item>
/// </list>
/// Seams (M11/M14): FaceWorld, the robot pose, BaseStationTimer seconds.
/// The strategy is the behaviour's <see cref="IReactToFaceListener"/> (ACKNOWLEDGE-FACE-1 T3, F2, S1); STBI copies the whole set into the
/// behaviour's +0x120 (T4).
/// </summary>
public sealed class FacePositionUpdatedStrategy : ReactionTriggerStrategy, IDisposable, IReactToFaceListener
{
    public const double CloseMm = 300.0, StayCloseMm = 400.0, CooldownSec = 4.0;

    private readonly FaceWorld _world;
    private readonly AcknowledgeFaceBehavior _behavior;
    private readonly Func<Pose3d?> _robotPose;
    private readonly Func<double> _clockSec;
    private readonly object _gate = new();
    private readonly SortedSet<int> _desired = new();
    private readonly HashSet<int> _reacted = new();
    private readonly Dictionary<int, bool> _isClose = new();
    private double _lastReactionSec = -1;

    public FacePositionUpdatedStrategy(FaceWorld world, AcknowledgeFaceBehavior behavior, Func<Pose3d?> robotPose, Func<double> clockSec)
    {
        _world = world; _behavior = behavior; _robotPose = robotPose; _clockSec = clockSec;
        world.FaceObserved += OnObserved;
        // fidelity: M10-003 -- ACKNOWLEDGE-FACE-1 T3: BehaviorThatStrategyWillTriggerInternal calls the behaviour's AddListener (vslot AP+0x34,
        // reloc 0x0102B870) with strategy+0x50 (0x0060D074..0x0060D080). The stack binds strategy and behaviour here, at construction.
        behavior.AddListener(this);
    }

    public override ReactionTrigger Trigger => ReactionTrigger.FacePositionUpdated;
    public override string Basis => "ReactionTriggerStrategyFacePositionUpdated ctor 0x60C62E..0x60C69C, STBI 0x60CAB0..0x60CB5E, " +
                                    "face observed 0x60CB84..0x60CE20 (300/300/400 mm, 4 s cooldown)";
    public override bool ShouldResumeLast => true;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    /// <summary>The engine's log lines (3e, 3f).</summary>
    public event Action<string>? Log;
    public SourceLogSink? SourceLog { get; set; }

    /// <summary>desiredFaces (+0x58).</summary>
    public IReadOnlyCollection<int> DesiredFaces { get { lock (_gate) return _desired.ToList(); } }

    /// <summary>3d.</summary>
    public bool AddDesiredFace(int id)
    {
        if (!IsReactionTriggerEnabled(ReactionTrigger.FacePositionUpdated)) return false;
        lock (_gate) return _desired.Add(id);
    }

    private void OnObserved(FaceObservation o) => HandleFaceObserved(o.Face.Id);

    /// <summary>3e.</summary>
    public void HandleFaceObserved(int id)
    {
        if (id < 0 || _world.GetFace(id) is not { } face) return;
        bool handled = false;
        bool reacted;
        lock (_gate) reacted = _reacted.Contains(id);
        if (face.HasName && !reacted)
        {
            AddDesiredFace(id);
            Log?.Invoke($"ReactionTriggerStrategyFacePositionUpdated: face {id} named for the first time");
            handled = true;
        }
        if (_robotPose() is not { } robot)
        {
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, "ReactionTriggerStrategyFacePositionUpdated", $"could not get the distance to face {id}");
            return;
        }
        var d = robot.Translation - face.HeadPose.Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        bool inMap, wasClose;
        double last;
        lock (_gate) { inMap = _isClose.TryGetValue(id, out wasClose); last = _lastReactionSec; }
        double threshold = inMap && wasClose ? StayCloseMm : CloseMm;
        bool isClose = dist < threshold;
        double now = _clockSec();
        bool cooldown = last >= 0 && now < last + CooldownSec;
        if (inMap && !wasClose && isClose && !handled && !cooldown)
        {
            AddDesiredFace(id);
            Log?.Invoke($"BehaviorAcknowledgeFace.FaceBecomeClose: face {id}");
        }
        lock (_gate) _isClose[id] = isClose;
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        List<int> set;
        lock (_gate) set = _desired.ToList();
        if (set.Count == 0) return false;
        _behavior.SetTargets(set);                                                                 // T4: AcknowledgeFace+0x120 = the set
        if (rc.Context.Robot.Sensors.OnChargerPlatform) return false;
        return behavior.IsRunnable(rc.Context);
    }

    /// <summary>3f: FinishedReactingToFace.</summary>
    public void FinishedReactingToFace(int id)
    {
        SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Debug, null, "ReactionTriggerStrategyPositionUpdate.RobotReactedToId", $"no record for {id}");
        lock (_gate)
        {
            _reacted.Add(id);
            _lastReactionSec = _clockSec();
        }
    }

    /// <summary>3f: ClearDesiredTargets.</summary>
    public void ClearDesiredTargets() { lock (_gate) _desired.Clear(); }

    /// <summary>3b.</summary>
    public override void EnabledStateChanged(BehaviorContext context, bool enabled) => ClearDesiredTargets();

    public void Dispose()
    {
        _world.FaceObserved -= OnObserved;
        _behavior.RemoveListener(this);
    }
}

// fidelity: M10-003
/// <summary>
/// <c>ReactionTriggerStrategyPetInitialDetection</c> (gap1 8, 0x611AE4..0x611CB6); flags (1, 1, 0).
/// <list type="bullet">
/// <item>STBI: RecentlyReacted (last +0x40 &gt; −1 and last + 60 s &gt; now) → UpdateReactedTo (add all current pet ids) and
/// false; else collect each pet not in reactedTo whose numTimesObserved &gt; 2; none → false; on the charger platform
/// (robot+0x34A) → false; else ReactToPet+0x11C = the targets, and IsRunnable.</item>
/// <item>BehaviorDidReact: reset the set, add the current and the reacted ids, last = now. Ctor: +0x40 = −1.</item>
/// </list>
/// C2-F4: InitReactedTo (0x611FA4) clears and refills the set, but no instruction in the shipped .so calls
/// it (whole-.text BL/BLX, relocation and pointer scan): it is not part of the reachable path, and it is not
/// called by EnabledStateChanged. The reachable set maintenance is UpdateReactedTo (0x611E1C), called by
/// ShouldTriggerBehaviorInternal (0x611AE4) and BehaviorDidReact (0x611E80), which the code matches.
/// The ReactToPet behaviour (M14) picks its own pet; the targets are handed over as <see cref="Targets"/>.
/// </summary>
// fidelity: M14-004
public sealed class PetInitialDetectionStrategy : ReactionTriggerStrategy, IDisposable
{
    public const double RecentlyReactedSec = 60.0;
    public const int MinTimesObserved = 3;

    private readonly PetWorld _world;
    private readonly HashSet<int> _reactedTo = new();
    private readonly object _gate = new();
    private readonly ReactToPetBehavior? _behavior;
    private readonly Func<double>? _clockSec;
    // M14-004: [this+0x40] is a binary32 time (ctor -1.0f); RecentlyReacted 0x00611DD0..0x00611E14 compares (last + 60.0f) with the
    // binary32 clock in float: vmov.f32 s2,#-1.0 / vcmpe / ble (not recently reacted if last <= -1), vadd.f32 s16, last, 0x42700000, vcmpe s16, now / gt.
    private float _lastReactedSec = -1f;
    private IReadOnlyCollection<int> _targets = Array.Empty<int>();

    public PetInitialDetectionStrategy(PetWorld world) { _world = world; }

    public PetInitialDetectionStrategy(PetWorld world, ReactToPetBehavior behavior, Func<double> clockSec) : this(world)
    {
        _behavior = behavior;
        _clockSec = clockSec;
        behavior.Reacted += OnReacted;
    }

    public override ReactionTrigger Trigger => ReactionTrigger.PetInitialDetection;
    public override string Basis => "ReactionTriggerStrategyPetInitialDetection STBI 0x611AE4..0x611CB6: RecentlyReacted 60 s (0x611DD0), " +
                                    "numTimesObserved > 2, robot+0x34A; BehaviorDidReact 0x611E80..0x611F28";
    public override bool ShouldResumeLast => true;
    public override bool CanInterruptOtherTriggeredBehavior => true;
    public override bool CanInterruptSelf => false;

    /// <summary>ReactToPet+0x11C as the strategy last set it.</summary>
    public IReadOnlyCollection<int> Targets { get { lock (_gate) return _targets; } }

    public bool RecentlyReacted(double nowSec) { lock (_gate) return RecentlyReactedF(_lastReactedSec, (float)nowSec); }

    // fidelity: M14-004
    internal static bool RecentlyReactedF(float last, float now)
    {
        if (!(last > -1f)) return false;                       // 0x00611DD4..0x00611DE0: ble (NaN too) returns false
        float limit = last + (float)RecentlyReactedSec;        // 0x00611DEC vadd.f32 with 0x42700000
        return limit > now;                                    // 0x00611DFC vcmpe / itt gt
    }

    private void OnReacted(int petId) => BehaviorDidReact(new[] { petId }, _clockSec?.Invoke() ?? 0);

    /// <summary>BehaviorDidReact: reset the set, add the current and the reacted ids, last = now.</summary>
    public void BehaviorDidReact(IEnumerable<int> reacted, double nowSec)
    {
        lock (_gate)
        {
            _reactedTo.Clear();
            foreach (var p in _world.Pets) _reactedTo.Add(p.Id);
            foreach (var id in reacted) _reactedTo.Add(id);
            _lastReactedSec = (float)nowSec;
        }
    }

    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        var pets = _world.Pets;
        List<int> targets;
        lock (_gate)
        {
            if (RecentlyReactedF(_lastReactedSec, (float)rc.NowSec))
            {
                foreach (var p in pets) _reactedTo.Add(p.Id);                                     // UpdateReactedTo
                return false;
            }
            targets = pets.Where(p => !_reactedTo.Contains(p.Id) && p.TimesObserved >= MinTimesObserved).Select(p => p.Id).ToList();
        }
        if (targets.Count == 0) return false;
        if (rc.Context.Robot.Sensors.OnChargerPlatform) return false;
        lock (_gate) _targets = targets;
        return behavior.IsRunnable(rc.Context);
    }

    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }

    public void Dispose()
    {
        if (_behavior is not null) _behavior.Reacted -= OnReacted;
    }
}

public sealed class ReactToPetBehavior : FaceBehavior
{
    public ReactToPetBehavior(VisionSystem v, string id = "ReactToPet") : base(id, "ReactToPet", v) { }

    /// <summary>
    /// The pet this run reacted to, raised when the reaction starts.
    /// <see cref="PetInitialDetectionStrategy"/> listens so it can record the id and start its minute, as
    /// the engine's UpdateReactedTo and the time at +0x40 do.
    /// </summary>
    public event Action<int>? Reacted;

    public int? TargetPetId { get; private set; }
    public AnimationTrigger? Trigger { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => V.Pets.Pets.Count > 0;

    public AnimationTrigger GetAnimationTrigger(PetType type, Random rnd)
    {
        if (rnd.Next(20) == 0) return AnimationTrigger.PetDetectionSneeze;
        return type == PetType.Cat ? AnimationTrigger.PetDetectionCat : AnimationTrigger.PetDetectionDog;
    }

    protected override void OnStart()
    {
        var pet = V.Pets.Pets.OrderByDescending(p => p.LastObservedTimestamp).FirstOrDefault();
        if (pet is null) { Log("ReactToPet.BeginIteration.NoValidTarget"); Finish(); return; }
        TargetPetId = pet.Id;
        Trigger = GetAnimationTrigger(pet.Type, Context.Random);
        Log($"ReactToPet.BeginIteration: Reacting to petID {pet.Id} type {pet.Type}");
        Reacted?.Invoke(pet.Id);
        // TurnTowardsImagePointAction: the angles the pixel subtends, with no distance in it at all -
        // see TurnTowardsImagePoint, which is Robot::ComputeTurnTowardsImagePointAngles 0x0051879C.
        var centre = pet.Rect.Center;
        RunFaceAction("TurnTowardsImagePoint",
                      async ct => await TurnTowardsImagePoint.RunAsync(V, centre.X, centre.Y, pet.LastObservedTimestamp, ct)
                                  ? FaceActionResult.Success : FaceActionResult.Abort,
                      _ => PlayTrigger(Trigger.Value, Finish));
    }
}

/// <summary>
/// <c>BehaviorPyramidThankYou</c> (0x005DE078..0x005DE320): runnable when a face is known and the pyramid's
/// top block is located; <c>InitInternal</c> runs a sequential compound: <c>TurnTowardsFaceAction(π)</c> to the
/// last observed face (when it is in the current origin), <c>TriggerAnimationAction(0x18 BuildPyramidThankUser)</c>,
/// <c>TurnTowardsObjectAction(π)</c> at the pyramid's block, and 0x18 again.
/// </summary>
public sealed class PyramidThankYouBehavior : FaceBehavior
{
    private readonly ManipulationSystem _m;
    public PyramidThankYouBehavior(VisionSystem v, ManipulationSystem m, string id = "PyramidThankYou") : base(id, "PyramidThankYou", v) => _m = m;

    protected override bool IsRunnableInternal(BehaviorContext context) => V.Faces.HasAnyFaces() && _m.NativePyramids.Count > 0;

    protected override void OnStart()
    {
        var pyramid = _m.NativePyramids.FirstOrDefault();
        if (pyramid is null) { Finish(); return; }
        // fidelity: M13-014 (0x005DE160..0x005DE170: a TurnTowardsLastFacePoseAction with +0x193 = 1)
        var turn = new TurnTowardsLastFacePoseAction(V, Math.PI, sayName: false) { RequireVerifiedFace = true };
        RunFaceAction("TurnTowardsFace(last)", turn.RunAsync, _ =>
            PlayTrigger(AnimationTrigger.BuildPyramidThankUser, () =>
                RunAction($"TurnTowardsObject({pyramid.TopBlockId})", ct => _m.TurnTowardsObjectAsync(pyramid.TopBlockId, Math.PI, ct), _ =>
                    PlayTrigger(AnimationTrigger.BuildPyramidThankUser, Finish), false)));
    }
}
