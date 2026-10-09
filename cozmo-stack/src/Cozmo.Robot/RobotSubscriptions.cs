using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M1-046
// Checked U1-U8. Managed storage replaces native allocations; retiring a handle
// removes its callback and never invokes it or emits a message.
// These internal handles are created/delivered/retired on the serialized engine
// thread. The native atomic allocation bookkeeping has no cross-thread managed owner.
internal sealed class RetainedSubscription : IDisposable
{
    private Action? _unsubscribe;
    private int _owners = 1;
    internal RetainedSubscription(Action unsubscribe) => _unsubscribe = unsubscribe;
    internal RetainedSubscription Retain() { ++_owners; return this; }
    public void Dispose()
    {
        if (_owners == 0 || --_owners != 0) return;
        var unsubscribe = _unsubscribe;
        _unsubscribe = null;
        unsubscribe?.Invoke(); // +8, then managed handle storage retirement (+4).
    }
}

// fidelity: M1-046
internal sealed class SubscriptionSignal<T>
{
    private sealed class Node
    {
        internal readonly int Tag;
        internal Action<T>? Callback;
        internal Node(int tag, Action<T> callback) { Tag = tag; Callback = callback; }
    }
    private readonly LinkedList<Node> _nodes = new();
    internal RetainedSubscription Subscribe(int tag, Action<T> callback)
    {
        var saved = _nodes.AddLast(new Node(tag, callback));
        var weak = new WeakReference<SubscriptionSignal<T>>(this);
        return new RetainedSubscription(() =>
        {
            // U4/U7: absent/expired signal or missing node => no action.
            if (!weak.TryGetTarget(out var signal) || saved.List != signal._nodes) return;
            // U5/U7: destroy callback, zero pointer, then unlink. A delivery
            // snapshot can retain the node storage, but cannot call its old callback.
            saved.Value.Callback = null;
            signal._nodes.Remove(saved);
        });
    }
    internal void Deliver(int tag, T message)
    {
        foreach (var node in _nodes.ToArray())
            if (node.Tag == tag) node.Callback?.Invoke(message);
    }
}

// fidelity: M1-046
internal sealed class SubscriptionOwners
{
    private readonly List<RetainedSubscription> _handles = new();
    internal void Add(RetainedSubscription handle) => _handles.Add(handle);
    internal void Retire()
    {
        // U1: vector end -= 8 before __release_shared, 004EAEA2..004EAEBA.
        for (int i = _handles.Count - 1; i >= 0; --i) _handles[i].Dispose();
        _handles.Clear();
    }
}

public sealed partial class EngineRobot
{
    private readonly SubscriptionSignal<RobotMessage> _robotMessages = new();
    private readonly SubscriptionSignal<byte> _idleMessages = new();
    private readonly SubscriptionOwners _messagingHandles = new(), _idleHandles = new();
    private bool _lastStateHandled;

    // Existing RobotToEngine handlers retain their original order and isolation.
    // The host API is the external-interface endpoint (AGENTS app boundary).
    // fidelity: M1-046
    private void InitSubscriptions()
    {
        // Idle is constructed before Messaging. The C# host API supplies the
        // external interface, so HasExternalInterface is true on this path.
        _idleHandles.Add(_idleMessages.Subscribe(0x54, _ => Idle.Cancel()));
        AddMessage(RobotMessageId.SyncTimeAck, _ => HandleSyncTimeAck());
        AddMessage(RobotMessageId.State, m => _lastStateHandled = UpdateFullRobotState((RobotState)m));
        AddMessage(RobotMessageId.CrashReport, _ => TracePrinterOnCrashReport());
        AddMessage(RobotMessageId.AnimState, m => HandleAnimationState((AnimationState)m));
        AddMessage(RobotMessageId.FirmwareVersion, m => HandleFirmwareVersion((FirmwareVersion)m));
        Lifetime.Bind(0x51C, _ => _idleHandles.Retire());
        Lifetime.Bind(0x518, _ => _messagingHandles.Retire());
    }
    private void AddMessage(RobotMessageId tag, Action<RobotMessage> callback)
        => _messagingHandles.Add(_robotMessages.Subscribe((int)tag, m => Engine.DeliverIsolated(() => callback(m))));
    internal bool DeliverMessage(RobotMessage message)
    {
        _lastStateHandled = message is not RobotState;
        _robotMessages.Deliver((int)message.Id, message);
        return _lastStateHandled;
    }
    internal void DeliverCancelIdleTimeout() => _idleMessages.Deliver(0x54, 0);
}

public sealed partial class CozmoEngine
{
    internal void DeliverIsolated(Action callback) => Isolated(callback);
}
