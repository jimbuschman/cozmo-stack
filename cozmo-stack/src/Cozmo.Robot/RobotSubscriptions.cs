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
// fidelity: M2-018 — per-tag signal, native tail append and next read AFTER the call.
internal sealed class SubscriptionSignal<T>
{
    private sealed class Node
    {
        internal Action<T>? Callback;
        internal Node Next = null!, Prev = null!;
    }
    private readonly Dictionary<int, Node> _signals = new();
    private readonly object _gate = new();
    internal RetainedSubscription Subscribe(int tag, Action<T> callback)
    {
        Node node;
        lock (_gate)
        {
            if (!_signals.TryGetValue(tag, out var sentinel))
            { sentinel = new Node(); sentinel.Next = sentinel.Prev = sentinel; _signals[tag] = sentinel; }
            node = new Node { Callback = callback, Next = sentinel, Prev = sentinel.Prev };
            sentinel.Prev.Next = node; sentinel.Prev = node;
        }
        var weak = new WeakReference<SubscriptionSignal<T>>(this);
        return new RetainedSubscription(() =>
        {
            if (!weak.TryGetTarget(out var signal)) return;
            lock (signal._gate)
            {
                if (node.Callback is null) return;
                node.Callback = null;
                node.Prev.Next = node.Next; node.Next.Prev = node.Prev;
                // Removed nodes retain their OWN links, including a currently executing node.
            }
        });
    }
    internal void Deliver(int tag, T message)
    {
        Node sentinel, node;
        lock (_gate) { if (!_signals.TryGetValue(tag, out sentinel!)) return; node = sentinel.Next; }
        while (node != sentinel)
        {
            Action<T>? callback;
            lock (_gate) callback = node.Callback;
            callback?.Invoke(message); // External callouts never hold the storage lock.
            lock (_gate) node = node.Next;
        }
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
    private readonly HashSet<RobotMessageId> _implTags = new();

    // Existing RobotToEngine handlers retain their original order and isolation.
    // The host API is the external-interface endpoint (AGENTS app boundary).
    // fidelity: M1-046
    private void InitSubscriptions()
    {
        // Idle is constructed before Messaging. The C# host API supplies the
        // external interface, so HasExternalInterface is true on this path.
        _idleHandles.Add(_idleMessages.Subscribe(0x54, _ => Idle.Cancel()));
        InitCheckedDevSubscribers();
        Engine.PathComponentConstructed?.Invoke(this); // M12-032: the component owns its one first C6 subscription.
        // Backup is the first C9 subscriber, constructed with NVStorage before Impl.
        AddMessage(RobotMessageId.RobotAvailable, _ => Engine.EmitStackLog("MISSING: M15-039 | Backup::RobotConnected 0x00519B78 -> 0x0051A010; B-M15-1"), "Backup");
        AddMessage(new ObjectAccel().Id, m => Engine.CubeAccelRoute?.Invoke(m), "CubeAccel");
        AddMessage(RobotMessageId.ActiveObjectMoved, m => Engine.TapFilterRoute?.Invoke(m), "BlockTapFilter");
        AddMessage(RobotMessageId.ActiveObjectStopped, m => Engine.TapFilterRoute?.Invoke(m), "BlockTapFilter");
        AddMessage(RobotMessageId.SyncTimeAck, _ => HandleSyncTimeAck());
        AddMessage(RobotMessageId.State, m => _lastStateHandled = UpdateFullRobotState((RobotState)m));
        AddMessage(RobotMessageId.CrashReport, _ => TracePrinterOnCrashReport());
        AddMessage(RobotMessageId.AnimState, m => HandleAnimationState((AnimationState)m));
        // fidelity: M3-055 — HandleRobotSetHeadID 0x5367D8 and HandleRobotSetBodyID 0x536FF0.
        AddMessage(RobotMessageId.RobotAvailable, m =>
        {
            var a = (RobotAvailable)m;
            string buf = $"0xbeef{a.HwVersion:x4}{a.SerialNumberHead:x8}";
            Engine.Das.SetGlobal("$group", buf);
            // fidelity: M3-060 — sEvent follows sSetGlobal, 0x00536848.
            Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.handle_robot_set_head_id", buf,
                new Dictionary<string, string> { ["$data"] = buf });
        });
        AddMessage(RobotMessageId.MfgId, m =>
        {
            var a = (ManufacturingID)m;
            string buf = $"0xbeef{a.BodyColor & 0xffff:x4}{a.BodyHwVersion & 0xffff:x4}{a.SerialNumber:x8}";
            Engine.Das.SetGlobal("$phys", buf);
            // fidelity: M3-060 — sEvent follows sSetGlobal, 0x00537028.
            Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.handle_robot_set_body_id", buf,
                new Dictionary<string, string> { ["$data"] = buf });
        });
        AddMessage(RobotMessageId.FirmwareVersion, m => HandleFirmwareVersion((FirmwareVersion)m));
        AddMessage(RobotMessageId.FactoryFirmwareVersion, m => HandleFWVersionInfo((FWVersionInfo)m));
        AddMessage(RobotMessageId.DockingStatus, m => HandleDockingStatus((DockingStatus)m));
        AddMessage(RobotMessageId.PickAndPlaceResult, m =>
        {
            if (Engine.PickAndPlaceRoute is { } route) route(m);
            else Engine.EmitStackLog("MISSING: M12-008 | Impl::HandlePickAndPlaceResult 0x00533780 has no attached DockingSystem recipient");
        });
        Lifetime.Bind(0x51C, _ => _idleHandles.Retire());
        // fidelity: M3-041
        // I2 0x00532BC8..0x00532BE8: HandleImuData (tag 0xBF) and HandleImuRawData (tag 0xC7) are subscribed with the same
        // retained-subscription helper as the handlers above, with no debug/SDK gate. The row set does not give their
        // place in Messaging::Init's order; it is observable nowhere (distinct tags, retirement invokes no callback).
        _imuLog = new ImuDiagnosticLog(this);
        AddMessage(RobotMessageId.ImuDataChunk, m => _imuLog.HandleImuData((IMUDataChunk)m));
        AddMessage(RobotMessageId.ImuRawDataChunk, m => _imuLog.HandleImuRawData((IMURawDataChunk)m));
        // L35/I9: ~Messaging releases the subscription vector (0x00532A48), then the filebuf destructor closes the IMU
        // stream (0x00532A64 -> 0x005010B4 -> close 0x0050111C): sync, fclose, no ClosingLogFile diagnostic.
        Lifetime.Bind(0x518, _ => { _messagingHandles.Retire(); _imuLog.Destroy(); });
        // Only the existing live device handlers are bridged here. No subscriber is invented for an unported tag.
        RobotMessage[] deviceMessages = { new ObjectMoved(), new ObjectStoppedMoving(), new ObjectTapped(),
            new ObjectConnectionState(), new ObjectPowerLevel(), new ObjectUpAxisChanged(), new ObjectAvailable(),
            new DefaultCameraParams(), new ImageChunk(), new ImageImuData(), new CliffEvent(), new PotentialCliff(),
            new RobotStopped(), new FallingStarted(), new FallingStopped(), new MotorCalibration() };
        foreach (var tag in deviceMessages.Select(m => m.Id))
            if (!_implTags.Contains(tag)) AddMessage(tag, _ => { },(byte)tag==0xB6?"BlockTapFilter":"Impl", routeDevice:true);
    }
    private ImuDiagnosticLog _imuLog = null!;
    internal ImuDiagnosticLog ImuLog => _imuLog;
    private void AddMessage(RobotMessageId tag, Action<RobotMessage> callback, string name = "Impl", bool routeDevice = false)
    {
        if (name == "Impl") _implTags.Add(tag);
        _messagingHandles.Add(SubscribeMessage(tag, m =>
        {
            callback(m);
            if (name == "Impl" || routeDevice) Engine.DeviceRoute?.Invoke(m, _lastStateHandled);
        }, name));
    }
    internal RetainedSubscription SubscribeMessage(RobotMessageId tag, Action<RobotMessage> callback, string name)
        => _robotMessages.Subscribe((int)tag, m => Engine.DeliverIsolated(() =>
        { Engine.MessageDispatchObservedForTest?.Invoke(tag, name); callback(m); }));
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
