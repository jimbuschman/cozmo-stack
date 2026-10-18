using System.Buffers.Binary;
namespace Cozmo.Robot;

/// <summary>Engine-to-game AnimationEvent (0x60): the robot animation tag is omitted.</summary>
public sealed record AnimationEventReport(uint Timestamp, byte EventId);

public sealed partial class CozmoEngine
{
    /// <summary>Animation events at the app boundary, before local engine delivery.</summary>
    public event Action<AnimationEventReport>? AnimationEvent;
    // fidelity: M8-020 — 0x00545750..0x00545792 -> UI rvalue Broadcast 0x006625B0.
    internal void BroadcastAnimationEvent(AnimationEventReport report)
    {
        byte[] payload = new byte[5];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, report.Timestamp); payload[4] = report.EventId;
        DeliverAnimationReportToApp(0x60, payload);
        if (AnimationEvent is {} handlers)
            foreach (Action<AnimationEventReport> handler in handlers.GetInvocationList()) RunIsolated(() => handler(report));
        // M2-022's unbuilt native recipients are not inferred from this delivery boundary.
        AnimationMessages.Deliver(0x60, true, report);
    }
    // Native DeliverToGame hands the payload to the app; the C# host API is inline (AGENTS app boundary).
    internal void DeliverAnimationReportToApp(ushort tag, ReadOnlyMemory<byte> payload)
    {
        NativeUiDeliveryCount = unchecked(NativeUiDeliveryCount + 1);
        if (EngineToGameMessage is {} handlers)
            foreach (Action<ushort,ReadOnlyMemory<byte>> handler in handlers.GetInvocationList()) RunIsolated(() => handler(tag, payload));
    }
}
