using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorDriveOffCharger</c> (ctor 0x005C0980, <c>IsRunnableInternal</c> 0x005C0B10, <c>InitInternal</c> 0x005C0B18, <c>TransitionToDrivingForward</c> 0x005C0BB8,
/// <c>UpdateInternal</c> 0x005C0DA8; config <c>extraDistanceToDrive_mm</c> 60 freeplay / 45 hiking).
///
/// <b>The gate is robot+0x34A</b>, <c>OnChargerPlatform</c> (<c>SetOnChargerPlatform</c> 0x00511D4C..0x00511D6A: <c>(arg != 0) || robot+0x338</c>), which
/// <c>Sensors.OnChargerPlatform</c> carries; it is not the +0x338 contact flag (<c>Sensors.OnCharger</c>). <c>IsRunnableInternal</c> returns it (0x005C0B10);
/// <c>TransitionToDrivingForward</c> drives only while it is set (0x005C0BF4); <c>UpdateInternal</c> ends the behaviour when it clears.
///
/// <c>InitInternal</c>: the reaction lock, +0x120 = 0, the driving animations when the animation state is 3 (not built), then the state name
/// "WaitForOnTreads" when robot+0x355 is set (the off-treads state) else <c>TransitionToDrivingForward</c> ("DrivingForward"). That transition starts a
/// <c>DriveStraightAction(robot, +0x11C)</c> with <c>StartActing</c> (0x005C0C08..0x005C0C26), +0x11C = <b>96.0f + extraDistanceToDrive_mm</b> in binary32
/// (<c>vadd.f32</c> at 0x005C09E2), and its completion callback (0x005C0E9C) does, only on result 0: <c>BehaviorObjectiveAchieved(4, true)</c> and
/// <c>MoodManager::TriggerEmotionEvent("DriveOffCharger", now)</c>, so the mood event is part of the drive's completion, not something that waits for the robot to be on its treads.
///
/// <c>UpdateInternal</c>: while on the platform, robot+0x355 set means <c>StopActing(false, false)</c> and the state name "WaitForOnTreads"; clear means
/// <c>TransitionToDrivingForward</c> when no action is current (+0x84 == 0); either way it returns 1 (running). Off the platform it returns 1 while an action is
/// current, otherwise it stores the current time at <c>[[robot+0x264]+0x18]+0x44</c> and returns 2 (complete). There is no timeout of the behaviour's own.
/// </summary>
// fidelity: M13-017
public sealed class DriveOffChargerBehavior : ManipulationBehavior
{
    public enum Phase { Idle, Driving, WaitForOnTreads }

    /// <summary>96.0f: the charger's length, 0x42C00000 (0x005C09D4), the constant the drive distance starts from.</summary>
    public static readonly float ChargerLengthMm = BitConverter.Int32BitsToSingle(0x42C00000);
    /// <summary>The <c>BehaviorObjective</c> the completion callback reports: 4 (<c>movs r1,#4</c> at 0x005C0EA8).</summary>
    public const int ObjectiveDriveOffCharger = 4;
    /// <summary>The emotion event the completion callback triggers (the 15-byte literal copied at 0x005C0EBC..0x005C0ED0).</summary>
    public const string EmotionEventName = "DriveOffCharger";

    public DriveOffChargerBehavior(ManipulationSystem m, string id = "DriveOffCharger", double extraDistanceToDriveMm = 60)
        : base(id, "DriveOffCharger", m)
    {
        ExtraDistanceMm = extraDistanceToDriveMm;
        DriveDistanceMm = ChargerLengthMm + (float)extraDistanceToDriveMm;       // Json::Value::asFloat, then vadd.f32 (0x005C09C0..0x005C09E6)
    }

    public double ExtraDistanceMm { get; }
    /// <summary>+0x11C: 96.0f + the config's extra distance, binary32.</summary>
    public float DriveDistanceMm { get; }
    public double DistanceMm => DriveDistanceMm;
    public Phase CurrentPhase { get; private set; }
    /// <summary>Terminal action result retained for conformance and callers that need more than phase entry.</summary>
    public ActionResult? DriveResult { get; private set; }
    /// <summary>Whether the behaviour ended on its own (<c>UpdateInternal</c> returned 2) with the robot back on its treads.</summary>
    public bool LeftChargerOnTreads { get; private set; }
    /// <summary>The time <c>UpdateInternal</c> stored at the whiteboard's +0x44 when the robot was off the platform, or null.</summary>
    public double? DroveOffAtSec { get; private set; }

    // the behaviour is alive until UpdateInternal returns 2, whether or not an action is running (it returns 1 while waiting for the treads)
    protected override bool KeepsRunningWithoutAction => true;

    // fidelity: M13-017
    protected override bool IsRunnableInternal(BehaviorContext context) => context.Robot.Sensors.OnChargerPlatform;      // ldrb robot+0x34A, 0x005C0B10

    protected override void OnStart()
    {
        // fidelity: M7-014
        // BehaviorDriveOffCharger::InitInternal 0x005C0B1C..0x005C0B2A: SmartDisableReactionsWithLock(own name, table 0x00C672F0).
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.DriveOffCharger);
        DriveResult = null;
        LeftChargerOnTreads = false;
        DroveOffAtSec = null;
        SteppedBehavior.ReportMissing("BehaviorDriveOffCharger::InitInternal DrivingAnimationHandler::PushDrivingAnimations (0x005C0B34..0x005C0B50, when the AI value is 3): not built");
        // fidelity: M7-021, M13-017
        // InitInternal 0x005C0B54..0x005C0B8C: robot+0x355 set takes the "WaitForOnTreads" state name (0x005C0B74), else TransitionToDrivingForward.
        if (Context.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads) { SetStateName("WaitForOnTreads"); CurrentPhase = Phase.WaitForOnTreads; }
        else TransitionToDrivingForward();
    }

    /// <summary><c>TransitionToDrivingForward</c> 0x005C0BB8.</summary>
    // fidelity: M13-017
    private void TransitionToDrivingForward()
    {
        SetStateName("DrivingForward");                                              // 0x005C0BE2
        if (!Context.Robot.Sensors.OnChargerPlatform) return;                        // 0x005C0BF4: robot+0x34A
        CurrentPhase = Phase.Driving;
        int handle = StartActing();                                                  // IBehavior::StartActing 0x005C0C26
        if (handle == 0) return;
        int epoch = CallbackEpoch;
        SteppedBehavior.ReportMissing("DriveStraightAction(Robot&, float) 0x005C0C08: the two-argument constructor's speed and animation defaults and its Init/CheckIfDone are not in the M13 inventory; this stack's DriveStraightAction default speed is used");
        RunAction($"DriveStraightAction({DriveDistanceMm:F0} mm)", ct => new DriveStraightAction(M, DriveDistanceMm).RunAsync(ct), r =>
        {
            ActingEnded(handle);                                                     // HandleActionComplete clears +0x84 first (0x005BE1FC)
            if (!CallbackMayRun(epoch)) return;
            DriveResult = r;
            OnDriveComplete(r);
        });
    }

    /// <summary>The completion callback 0x005C0E9C: on result 0 only, <c>BehaviorObjectiveAchieved(4, true)</c> then <c>TriggerEmotionEvent("DriveOffCharger", now)</c>.</summary>
    // fidelity: M13-017
    private void OnDriveComplete(ActionResult result)
    {
        if (result != ActionResult.Success) return;                                  // cbnz r0 at 0x005C0EA4
        Log($"BehaviorObjectiveAchieved({ObjectiveDriveOffCharger}, true)");
        SteppedBehavior.ReportMissing("IBehavior::BehaviorObjectiveAchieved(BehaviorObjective, bool) body is not in the inventory; BehaviorDriveOffCharger's completion callback (0x005C0EAC) only traces the call");
        EmotionEvent(EmotionEventName);                                              // MoodManager::TriggerEmotionEvent 0x005C0EE2
    }

    /// <summary><c>UpdateInternal</c> 0x005C0DA8.</summary>
    // fidelity: M13-017
    protected override void OnUpdate()
    {
        var sensors = Context.Robot.Sensors;
        if (sensors.OnChargerPlatform)                                               // ldrb robot+0x34A (0x005C0DB0)
        {
            if (sensors.OffTreadsState != OffTreadsState.OnTreads)                   // robot+0x355 (0x005C0DB6)
            {
                CancelAction();                                                      // the host task behind the action
                StopActing();                                                        // IBehavior::StopActing(false, false) 0x005C0DC4
                SetStateName("WaitForOnTreads");                                     // 0x005C0DE0
                CurrentPhase = Phase.WaitForOnTreads;
                return;
            }
            if (!HasCurrentAction) TransitionToDrivingForward();                     // [this+0x84] == 0 (0x005C0E0E..0x005C0E18)
            return;
        }
        if (HasCurrentAction) return;                                                // 0x005C0DF4: still acting, return 1
        // BaseStationTimer::GetCurrentTimeInSeconds -> [[robot+0x264]+0x18]+0x44 (0x005C0DFA..0x005C0E08), then return 2
        double now = Context.ClockSec?.Invoke() ?? Clock() / 1000.0;
        DroveOffAtSec = now;
        Context.LastDriveOffChargerSec = (float)now;                                 // [[robot+0x264]+0x18]+0x44: a float (GetCurrentTimeInSeconds), read by IsRunnableBase 0x005BD93C
        LeftChargerOnTreads = sensors.OffTreadsState == OffTreadsState.OnTreads;
        CurrentPhase = Phase.Idle;
        Finish();
    }
}

/// <summary>
/// <c>BehaviorReactToOnCharger</c> (0x00606C94..; config <c>timeTilSleepAnimation_s</c> 300,
/// <c>timeTilDisconnection_s</c> 330, <c>triggeredFromVoiceCommand</c> for <c>VC_GoToSleep</c>): runnable when the
/// robot is on the charger (the reaction table maps <c>PlacedOnCharger</c> to it), or when the voice command
/// requests it. <c>InitInternal</c> pushes the idle animation 0x23F (none) and plays 0x189
/// <see cref="AnimationTrigger.PlacedOnCharger"/> with <c>TriggerLiftSafeAnimationAction</c>; then it stays until
/// the robot leaves the charger, broadcasting <c>GoingToSleep</c> to the app at the sleep time and
/// <c>StartIdleTimeout</c> (the app disconnects) at the disconnection time. Those two are engine-to-app
/// messages; this stack has no app, so they are logged (the robot-side sleep animation is the app's to
/// request). Objective <c>ReactedToOnCharger</c>.
/// </summary>
public sealed class ReactToOnChargerBehavior : SteppedBehavior
{
    public enum Phase { Idle, PlayingReaction, WaitingOnCharger, SleepAnnounced, DisconnectAnnounced }

    public ReactToOnChargerBehavior(string id = "ReactToOnCharger", double timeTilSleepAnimationSec = 300, double timeTilDisconnectionSec = 330, bool triggeredFromVoiceCommand = false)
        : base(id, "ReactToOnCharger")
    {
        TimeTilSleepSec = timeTilSleepAnimationSec; TimeTilDisconnectionSec = timeTilDisconnectionSec; TriggeredFromVoiceCommand = triggeredFromVoiceCommand;
    }

    public double TimeTilSleepSec { get; }
    public double TimeTilDisconnectionSec { get; }
    public bool TriggeredFromVoiceCommand { get; }
    /// <summary>For the voice-command variant: set when the command arrives (this stack has no voice pipeline; tests and tools set it).</summary>
    public bool Requested { get; set; }
    public Phase CurrentPhase { get; private set; }
    public IReadOnlyList<string> Broadcasts => _broadcasts;
    private readonly List<string> _broadcasts = new();
    private double _startMs;

    protected override bool KeepsRunningWithoutAction => true;

    protected override bool IsRunnableInternal(BehaviorContext context) => TriggeredFromVoiceCommand ? Requested : context.Robot.Sensors.OnCharger;

    protected override void OnStart()
    {
        // fidelity: M7-014
        // BehaviorReactToOnCharger::InitInternal 0x00606C9C..0x00606CB0: SmartDisableReactionsWithLock(own name, table 0x00C74182), first.
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.ReactToOnCharger);
        Requested = false;
        _broadcasts.Clear();
        _startMs = NowMs;
        CurrentPhase = Phase.PlayingReaction;
        Log("SmartPushIdleAnimation(Count): no idle while on the charger");
        PlayTrigger(AnimationTrigger.PlacedOnCharger, () =>
        {
            Log("objective achieved: ReactedToOnCharger");
            CurrentPhase = Phase.WaitingOnCharger;
        });
    }

    protected override void OnUpdate()
    {
        if (CurrentPhase is Phase.Idle or Phase.PlayingReaction) return;
        if (!Context.Robot.Sensors.OnCharger && !TriggeredFromVoiceCommand) { Log("off the charger"); CurrentPhase = Phase.Idle; Finish(); return; }
        double elapsed = (NowMs - _startMs) / 1000.0;
        if (CurrentPhase == Phase.WaitingOnCharger && elapsed >= TimeTilSleepSec)
        {
            CurrentPhase = Phase.SleepAnnounced;
            _broadcasts.Add("GoingToSleep");
            Log($"GoingToSleep broadcast at {elapsed:F0} s (engine-to-app; the app plays the sleep animation)");
        }
        if (CurrentPhase == Phase.SleepAnnounced && elapsed >= TimeTilDisconnectionSec)
        {
            CurrentPhase = Phase.DisconnectAnnounced;
            _broadcasts.Add("StartIdleTimeout");
            Log($"StartIdleTimeout broadcast at {elapsed:F0} s (engine-to-app; the app disconnects)");
        }
    }

    protected override void OnStop(BehaviorStopReason reason) => CurrentPhase = Phase.Idle;
}

/// <summary>
/// <c>MountCharger</c> as a behaviour: the dev <c>DockingTestSimple</c> config docks with whatever is in
/// front of the robot; with a located charger the engine's action is <see cref="MountChargerAction"/>. This
/// wraps it for the manager and the conformance tool (runnable with a located charger, not on it).
/// </summary>
public sealed class MountChargerBehavior : ManipulationBehavior
{
    public MountChargerBehavior(ManipulationSystem m, string id = "DockingTestSimple") : base(id, "DockingTestSimple", m) { }
    public ActionResult? Result { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) =>
        !context.Robot.Sensors.OnCharger && M.World.GetLocatedObjectById(ChargerGeometry.ObjectId) is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var act = new MountChargerAction(M, ChargerGeometry.ObjectId);
        RunAction("MountChargerAction", act.RunAsync, r => { foreach (var l in act.Trace) Log("  " + l); Result = r; Finish(); });
    }
}
