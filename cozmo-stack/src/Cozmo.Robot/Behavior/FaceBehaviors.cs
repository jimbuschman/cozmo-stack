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
        Task.Run(() => action(cts.Token)).ContinueWith(t =>
        {
            var r = t.Status == TaskStatus.RanToCompletion ? t.Result : failed;
            Post(() =>
            {
                if (_cancel != cts) return;          // stopped meanwhile
                _cancel = null;
                Log($"{what} -> {r}");
                onDone(r);
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
        var turn = new TurnTowardsFaceAction(V, SmartFaceID.Invalid, Math.PI, sayName: false);
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
/// <c>BehaviorAcknowledgeFace</c> (0x00602950..0x00602CA0; reactions/acknowledgeFace.json): <c>BeginIteration</c>
/// picks the target with <c>AIWhiteboard::GetBestFaceToTrack</c> (the face nearest the robot's heading; LOCAL:
/// the most recently seen face not yet acknowledged), runs <c>TurnTowardsFaceAction(id, π, sayName)</c> where
/// the greeting is skipped when the robot already turned to this face and did so within 60 s
/// (<c>HasTurnedTowardsFace</c>, 0x42700000: "currTime = %f, alreadyTurned:%d, shouldPlayGreeting:%d"), with the
/// say-name trigger <see cref="AnimationTrigger.AcknowledgeFaceNamed"/> (1) and the no-name trigger
/// <see cref="AnimationTrigger.AcknowledgeFaceUnnamed"/> (2). <c>FinishIteration</c> reports the objective
/// <c>ReactedAcknowledgedFace</c>. The reaction fires on a new face observation (the manager's
/// <c>FacePositionUpdated</c> trigger).
/// </summary>
public sealed class AcknowledgeFaceBehavior : FaceBehavior
{
    public const double GreetingCooldownSec = 60.0;
    private readonly Dictionary<int, double> _lastGreetedSec = new();
    private readonly HashSet<int> _acknowledged = new();

    public AcknowledgeFaceBehavior(VisionSystem v, string id = "AcknowledgeFace") : base(id, "AcknowledgeFace", v)
    {
        // a face that is forgotten and seen again is a new acknowledgement
        v.Faces.FaceDeleted += id2 => { lock (_acknowledged) _acknowledged.Remove(id2); };
    }
    public int? TargetFaceId { get; private set; }
    public bool? PlayedGreeting { get; private set; }

    /// <summary>
    /// The engine runs this as the reaction to <c>FacePositionUpdated</c> (a face newly seen or moved); this stack
    /// re-arms it per face: a face is a candidate until acknowledged, and again when <see cref="ReArm"/> is called
    /// (the caller's position-updated trigger) or the face world forgets and re-learns it. LOCAL_POLICY.
    /// </summary>
    public void ReArm(int faceId) { lock (_acknowledged) _acknowledged.Remove(faceId); }

    private FaceEntry? BestFace()
    {
        lock (_acknowledged) return V.Faces.Faces.Where(f => !_acknowledged.Contains(f.Id)).OrderByDescending(f => f.LastObservedTimestamp).FirstOrDefault();
    }

    protected override bool IsRunnableInternal(BehaviorContext context) => BestFace() is not null;

    protected override void OnStart()
    {
        var face = BestFace();
        if (face is null) { Finish(); return; }
        TargetFaceId = face.Id;
        lock (_acknowledged) _acknowledged.Add(face.Id);
        double nowSec = Clock() / 1000.0;
        bool alreadyTurned = V.Faces.HasTurnedTowardsFace(face.Id) && _lastGreetedSec.TryGetValue(face.Id, out var t) && nowSec - t < GreetingCooldownSec;
        bool greet = !alreadyTurned;
        Log($"AcknowledgeFace.DoAcknowledgement: currTime = {nowSec:F1}, alreadyTurned:{(alreadyTurned ? 1 : 0)}, shouldPlayGreeting:{(greet ? 1 : 0)}");
        var turn = new TurnTowardsFaceAction(V, face.Id, Math.PI, sayName: greet) { SayNameTrigger = AnimationTrigger.AcknowledgeFaceNamed, NoNameTrigger = AnimationTrigger.AcknowledgeFaceUnnamed };
        turn.EmotionEvent += EmotionEvent;
        RunFaceAction($"TurnTowardsFace({face.Id})", turn.RunAsync, r =>
        {
            foreach (var l in turn.Trace) Log("  " + l);
            PlayedGreeting = greet && turn.Reaction is not null;
            if (greet) _lastGreetedSec[face.Id] = Clock() / 1000.0;
            PlayReaction(turn.Reaction, () => { Log("objective achieved: ReactedAcknowledgedFace"); Finish(); });
        });
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
        var turn = new TurnTowardsFaceAction(V, TargetFaceId.Value, Math.PI, sayName: true) { SayNameTrigger = AnimationTrigger.InteractWithFacesInitialNamed, NoNameTrigger = AnimationTrigger.InteractWithFacesInitialUnnamed };
        turn.EmotionEvent += EmotionEvent;
        RunFaceAction($"VerifyFace: TurnTowardsFace({TargetFaceId})", turn.RunAsync, r =>
        {
            foreach (var l in turn.Trace) Log("  " + l);
            if (r != FaceActionResult.Success) { Log($"initial reaction failed: {r}"); Finish(); return; }
            PlayReaction(turn.Reaction, TransitionToGlancingDown);
        });
    }

    private void TransitionToGlancingDown()
    {
        CurrentPhase = Phase.GlancingDown;
        if (_m is null) { Log("no manipulation system: skipping the glance and the drive forward"); TransitionToTrackingFace(); return; }
        CurrentPhase = Phase.DrivingForward;
        // CanDriveIdealDistanceForward reads the memory map (not modelled: DEFERRED); the ideal 40 mm is driven
        Tracker = new TrackFaceAction(V, TargetFaceId!.Value) { PanToleranceRad = TrackToleranceRad, TiltToleranceRad = TrackToleranceRad };
        RunAction($"DriveStraight({DriveForwardMm} mm) with TrackFace", async ct =>
        {
            using var trackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var track = Tracker.RunAsync(TimeSpan.FromSeconds(30), trackCts.Token);
            var r = await new DriveStraightAction(_m, DriveForwardMm, 50f).RunAsync(ct);
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
            var t1 = new TurnTowardsFaceAction(V, face.Id); var r = await t1.RunAsync(ct); foreach (var l in t1.Trace) Log("  " + l);
            if (r != FaceActionResult.Success) return r;
            var v = await new VisuallyVerifyFaceAction(V, face.Id).RunAsync(ct);
            Log($"  VisuallyVerifyFace -> {v}");
            var t2 = new TurnTowardsFaceAction(V, face.Id); return await t2.RunAsync(ct);
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
public sealed class ReactToPetBehavior : FaceBehavior
{
    public ReactToPetBehavior(VisionSystem v, string id = "ReactToPet") : base(id, "ReactToPet", v) { }
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
        // TurnTowardsImagePointAction: aim the head/body at the ray through the rectangle centre (200 mm out, INFERRED range)
        var cam = V.CurrentCamera();
        if (cam is not null)
        {
            var (o, d) = cam.Ray(pet.Rect.Center);
            var target = new Pose3d(Mat3.Identity, o + d.Normalized() * 200);
            RunFaceAction("TurnTowardsImagePoint", ct => new TurnTowardsPoseAction(V, target).RunAsync(ct), _ => PlayTrigger(Trigger.Value, Finish));
        }
        else PlayTrigger(Trigger.Value, Finish);
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

    protected override bool IsRunnableInternal(BehaviorContext context) => V.Faces.HasAnyFaces() && _m.Configurations.Pyramids.Count > 0;

    protected override void OnStart()
    {
        var pyramid = _m.Configurations.Pyramids.FirstOrDefault();
        if (pyramid is null) { Finish(); return; }
        var turn = new TurnTowardsFaceAction(V, SmartFaceID.Invalid);
        RunFaceAction("TurnTowardsFace(last)", turn.RunAsync, _ =>
            PlayTrigger(AnimationTrigger.BuildPyramidThankUser, () =>
                RunAction($"TurnTowardsObject({pyramid.TopBlockId})", ct => _m.TurnTowardsObjectAsync(pyramid.TopBlockId, Math.PI, ct), _ =>
                    PlayTrigger(AnimationTrigger.BuildPyramidThankUser, Finish), false)));
    }
}
