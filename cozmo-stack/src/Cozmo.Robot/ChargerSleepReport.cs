using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

public sealed record GoingToSleepReport(bool TriggeredFromVoiceCommand);
public sealed record ChargerSleepCancelledReport(byte ReactionTrigger);

public sealed partial class EngineRobot
{
    internal CozmoRobot? ChargerSleepAppRecipient;
}

public sealed partial class CozmoEngine
{
    public event Action<GoingToSleepReport>? GoingToSleep;
    public event Action<ChargerSleepCancelledReport>? ChargerSleepCancelled;

    // 0x00606CB4..0x00606CBE and 0x00606D80..0x00606DA8 emit E2G 0x2D.
    // GoingToSleep::Pack 0x00710D1A writes the single configured bool.
    internal void BroadcastGoingToSleep(EngineRobot owner, CozmoRobot facade, bool triggeredFromVoiceCommand)
    {
        if (!ReferenceEquals(Robot, owner) || owner.Lifetime.Destroying) return;
        var report = new GoingToSleepReport(triggeredFromVoiceCommand);
        owner.ChargerSleepAppRecipient = facade;
        DeliverAnimationReportToApp(0x2D, new[] { triggeredFromVoiceCommand ? (byte)1 : (byte)0 },
            CaptureAppEvent(GoingToSleep, report));
        AnimationMessages.Deliver(0x2D, true, report);
    }

    // Host replacement for PauseManager.cs:186-192, 320-333, 365-379, 484-491.
    // The pending notice stands for the confirm dialog. The first eligible
    // reaction posts the app inputs against the owner that received the notice.
    private void ObserveChargerSleepReaction(EngineRobot owner, ReactionTrigger? trigger)
    {
        if (trigger is not { } next || next == ReactionTrigger.PlacedOnCharger ||
            owner.ChargerSleepAppRecipient is not { } facade) return;
        owner.ChargerSleepAppRecipient = null;

        bool Current() => ReferenceEquals(Robot, owner) && !owner.Lifetime.Destroying;
        PostGameMessage(() =>
        {
            if (!Current()) return;
            facade.Lights.Cubes.EnableCubeSleep(false);
            if (!Current()) return;
            owner.DeliverCancelIdleTimeout();
            if (!Current()) return;
            owner.BehaviorManagerOwner?.RemoveDisableReactionsLock("pause_manager");
            if (!Current()) return;
            // Robot.cs:667-670 forwards RobotResumeFromIdle to SetEnableFreeplayActivity.
            // PauseManager initializes AllowFreeplayOnResume to true at line 132.
            facade.SetEnableFreeplayActivity(true);
            QueueAppEvent(ChargerSleepCancelled, new ChargerSleepCancelledReport((byte)next));
        });
    }
}
