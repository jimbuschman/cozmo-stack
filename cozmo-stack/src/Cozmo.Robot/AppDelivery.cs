using System.Collections.Generic;
namespace Cozmo.Robot;

// fidelity: M8-020, M5-023, M11-070, M1-055, M1-056
// Broadcast6625B0 enqueues through DeliverToGame661594 before the synchronous local emit.
public sealed partial class CozmoEngine
{
    private readonly object _appDeliveryGate=new();
    private readonly Queue<Action> _appDeliveries=new();
    private int _appDeliveryDraining;
    internal Func<bool>? AnimationMutationLockHeld;
    internal Action? WaitForAnimationMutation;

    internal Action? CaptureAppEvent<T>(Action<T>? handlers,T report)
    {
        if(handlers is null)return null;
        var subscribers=handlers.GetInvocationList();
        return ()=>{foreach(Action<T> subscriber in subscribers)RunIsolated(()=>subscriber(report));};
    }
    private void QueueAppDelivery(Action? delivery)
    {
        if(delivery is null)return;
        lock(_appDeliveryGate){if(!_disposed)_appDeliveries.Enqueue(delivery);}
    }
    internal void QueueAppEvent<T>(Action<T>? handlers,T report)=>QueueAppDelivery(CaptureAppEvent(handlers,report));

    internal Action? CaptureAppEvent<T1,T2>(Action<T1,T2>? handlers,T1 first,T2 second)
    {
        if(handlers is null)return null;
        var subscribers=handlers.GetInvocationList();
        return ()=>{foreach(Action<T1,T2> subscriber in subscribers)RunIsolated(()=>subscriber(first,second));};
    }
    internal void QueueAppEvent(Action? handlers)
    {
        if(handlers is null)return;
        var subscribers=handlers.GetInvocationList();
        QueueAppDelivery(()=>{foreach(Action subscriber in subscribers)RunIsolated(subscriber);});
    }
    internal void QueueAppBroadcast<T>(Action<T>? handlers,T report)
    {
        lock(_appDeliveryGate)
        {
            NativeUiDeliveryCount=unchecked(NativeUiDeliveryCount+1);
            QueueAppEvent(handlers,report);
        }
    }
    internal void QueueAppBroadcast(Action? handlers)
    {
        lock(_appDeliveryGate)
        {
            NativeUiDeliveryCount=unchecked(NativeUiDeliveryCount+1);
            QueueAppEvent(handlers);
        }
    }
    internal void QueueAppBroadcast<T1,T2>(Action<T1,T2>? handlers,T1 first,T2 second)
    {
        lock(_appDeliveryGate)
        {
            NativeUiDeliveryCount=unchecked(NativeUiDeliveryCount+1);
            QueueAppDelivery(CaptureAppEvent(handlers,first,second));
        }
    }

    // The batch is fixed before dispatch. A handler's new publications wait for the next boundary,
    // even when it calls an API that synchronously kicks the offline engine again.
    internal void DrainAppDelivery()
    {
        if(Monitor.IsEntered(_tickLock)||AnimationMutationLockHeld?.Invoke()==true ||
            Interlocked.CompareExchange(ref _appDeliveryDraining,1,0)!=0)return;
        try
        {
            Action[] batch;
            lock(_appDeliveryGate){batch=_appDeliveries.ToArray();_appDeliveries.Clear();}
            if(batch.Length==0)return;
            // Wait out the mutations which published this batch; hold neither lock during host code.
            // Snapshot first, so a later concurrent publication cannot join this already-fenced batch.
            lock(_tickLock){}
            WaitForAnimationMutation?.Invoke();
            foreach(var delivery in batch)
            {
                if(_disposed)break;
                RunIsolated(delivery);
            }
        }
        finally{Volatile.Write(ref _appDeliveryDraining,0);}
    }
    private void ClearAppDelivery(){lock(_appDeliveryGate)_appDeliveries.Clear();}
}
