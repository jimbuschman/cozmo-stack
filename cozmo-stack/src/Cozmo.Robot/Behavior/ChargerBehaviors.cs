using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorDriveOffCharger</c> (0x005C09xx..0x005C0C10; config <c>extraDistanceToDrive_mm</c> 60 freeplay /
/// 45 hiking): runnable while the robot reports IS_ON_CHARGER. It runs a <see cref="DriveOffChargerContactsAction"/>
/// for the charger's length (96) plus the extra distance, waits for the robot to be back on its treads
/// (<c>WaitForOnTreads</c>), and fires the emotion event "DriveOffCharger" (charger_events.json: Confident +0.3).
/// </summary>
public sealed class DriveOffChargerBehavior : ManipulationBehavior
{
    public enum Phase { Idle, Driving, WaitForOnTreads }

    public DriveOffChargerBehavior(ManipulationSystem m, string id = "DriveOffCharger", double extraDistanceToDriveMm = 60)
        : base(id, "DriveOffCharger", m) => ExtraDistanceMm = extraDistanceToDriveMm;

    public double ExtraDistanceMm { get; }
    public double DistanceMm => ChargerGeometry.LengthMm + ExtraDistanceMm;
    public Phase CurrentPhase { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => context.Robot.Sensors.OnCharger;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        CurrentPhase = Phase.Driving;
        var act = new DriveOffChargerContactsAction(M, DistanceMm);
        RunAction($"DriveOffChargerContactsAction({DistanceMm:F0} mm)", act.RunAsync, r =>
        {
            foreach (var l in act.Trace) Log("  " + l);
            CurrentPhase = Phase.WaitForOnTreads;
            WaitUntil(() => M.Robot.Sensors.OffTreadsState == OffTreadsState.OnTreads && !M.Robot.Sensors.OnCharger, 5.0, ok =>
            {
                double nowSec = Clock() / 1000.0;
                bool known = Context.Mood?.Trigger("DriveOffCharger", nowSec) ?? false;
                Log($"emotion event DriveOffCharger: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
                if (!ok) Log("still not on treads / off the charger after 5 s");
                CurrentPhase = Phase.Idle;
                Finish();
            }, "on treads and off the charger");
        });
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
