using System.Buffers.Binary;
namespace Cozmo.Robot;

/// <summary>Engine-to-game AnimationEvent (0x60): the robot animation tag is omitted.</summary>
public sealed record AnimationEventReport(uint Timestamp, byte EventId);

public sealed partial class CozmoEngine
{
    /// <summary>Animation event app view, dispatched after the synchronous local engine work.</summary>
    public event Action<AnimationEventReport>? AnimationEvent;
    // fidelity: M8-020 — 0x00545750..0x00545792 -> UI rvalue Broadcast 0x006625B0.
    internal void BroadcastAnimationEvent(AnimationEventReport report)
    {
        byte[] payload = new byte[5];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, report.Timestamp); payload[4] = report.EventId;
        DeliverAnimationReportToApp(0x60,payload,CaptureAppEvent(AnimationEvent,report));
        // M2-022's unbuilt native recipients are not inferred from this delivery boundary.
        AnimationMessages.Deliver(0x60, true, report);
    }
    // One native publication, with adjacent raw/typed host views; local emit remains at the caller.
    internal void DeliverAnimationReportToApp(ushort tag,ReadOnlyMemory<byte> payload,Action? typed=null,bool typedFirst=false,Action? beforeDispatch=null)
    {
        byte[] copy=payload.ToArray();
        var handlers=EngineToGameMessage?.GetInvocationList();
        lock(_appDeliveryGate)
        {
            NativeUiDeliveryCount=unchecked(NativeUiDeliveryCount+1);
            if(_disposed || (handlers is null && typed is null && beforeDispatch is null))return;
            _appDeliveries.Enqueue(()=>
            {
                beforeDispatch?.Invoke();
                if(typedFirst)typed?.Invoke();
                if(handlers is not null)
                    foreach(Action<ushort,ReadOnlyMemory<byte>> handler in handlers)RunIsolated(()=>handler(tag,copy));
                if(!typedFirst)typed?.Invoke();
            });
        }
    }
}
