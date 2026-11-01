using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

/// <summary>Engine-to-game reaction transition; 0x16 represents NoneTrigger.</summary>
public sealed record ReactionTransitionReport(byte OldTrigger, byte NewTrigger);
internal sealed record DefaultHeadAndLiftState(bool Enable, float HeadRadians, float LiftHeightMm);

public sealed partial class CozmoEngine
{
    public event Action<ReactionTransitionReport>? ReactionTransition;
    // fidelity: M8-012: 0x005A20AA..D6, 0x005A2EE8; E2G 0x88 old then new.
    internal void BroadcastReactionTransition(ReactionTrigger? oldTrigger, ReactionTrigger? newTrigger)
    {
        var report = new ReactionTransitionReport(oldTrigger is { } old ? (byte)old : (byte)0x16,
            newTrigger is { } next ? (byte)next : (byte)0x16);
        DeliverAnimationReportToApp(0x88, new byte[] { report.OldTrigger, report.NewTrigger }, CaptureAppEvent(ReactionTransition, report));
        AnimationMessages.Deliver(0x88, true, report);
    }
}
