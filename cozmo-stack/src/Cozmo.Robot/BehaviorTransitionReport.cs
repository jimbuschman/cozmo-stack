using System.Runtime.CompilerServices;

namespace Cozmo.Robot;

/// <summary>BehaviorTransition engine-to-game report (0x87).</summary>
public sealed record BehaviorTransitionReport(byte OldId,byte NewId,byte OldClass,byte OldExecutable,byte NewClass,byte NewExecutable,string DisplayKey);

internal sealed record BehaviorTransitionContext(string? Activity, string Reason);

public sealed partial class CozmoEngine
{
    public event Action<BehaviorTransitionReport>? BehaviorTransition;
    private readonly ConditionalWeakTable<BehaviorTransitionReport, BehaviorTransitionContext> behaviorTransitionContexts = new();

    // Host presentation captures the reason at the switch. The native 0x87 payload is unchanged.
    internal BehaviorTransitionContext? GetBehaviorTransitionContext(BehaviorTransitionReport report) =>
        behaviorTransitionContexts.TryGetValue(report, out var context) ? context : null;
    // 0x0071B806..0x0071B884 packs the six endpoint bytes and the length-prefixed display key.
    internal void BroadcastBehaviorTransition(BehaviorTransitionReport report, BehaviorTransitionContext? context = null)
    {
        if (context is not null) behaviorTransitionContexts.Add(report, context);
        byte[] key=System.Text.Encoding.UTF8.GetBytes(report.DisplayKey);
        if(key.Length>255)throw new ArgumentOutOfRangeException(nameof(report));
        byte[] payload=new byte[7+key.Length];
        payload[0]=report.OldId;payload[1]=report.NewId;payload[2]=report.OldClass;payload[3]=report.OldExecutable;
        payload[4]=report.NewClass;payload[5]=report.NewExecutable;payload[6]=(byte)key.Length;key.CopyTo(payload,7);
        DeliverAnimationReportToApp(0x87,payload,CaptureAppEvent(BehaviorTransition,report));
        AnimationMessages.Deliver(0x87,true,report);
    }
}
