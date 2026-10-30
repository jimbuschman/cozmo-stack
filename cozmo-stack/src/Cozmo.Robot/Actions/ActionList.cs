namespace Cozmo.Robot;

/// <summary>
/// The engine's <c>ActionList</c> (20261004-actionlist-extraction.md rows A1/A3/A6/A7/A8/A9, T5..T8, Q1..Q15,
/// C1..C6): an ordered signed-int key -> <see cref="ActionQueue"/> map, a clearing re-entry flag, a watcher, and
/// the QueueAction dispatch for all six shipped positions.
///
/// Native offsets are identification, not proposed C# layout: map +4, clearing +0xC, watcher +0x10, count +8.
/// </summary>
internal sealed class ActionList : IDisposable
{
    private readonly SortedDictionary<int, ActionQueue> _queues = new();
    private readonly ActionWatcher _watcher = new();
    private readonly Action<string>? _log;
    private readonly SourceLogSink? _sourceLog;
    private Action<RobotCompletedAction>? _gameSend;
    private bool _clearing;
    // The engine is single-threaded, so this lock changes no engine behaviour: it only keeps a caller that queues
    // from another thread (the stack's async FlipBlockAction drives SetLiftHeightAsync off the pump thread) from
    // mutating _queues while ActionList::Update enumerates it. A SortedDictionary modification during enumeration
    // throws InvalidOperationException; the same lock also serialises the ActionQueue internals and Cancel/Clear.
    private readonly object _gate = new();
    private readonly Func<IDisposable>? _enterOwner;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _hostCleanup = new();
    // Host availability errors retire their own subtree after queue iteration, independently of app input.
    internal void DeferHostCleanup(Action cleanup) => _hostCleanup.Enqueue(cleanup);

    internal void WithGate(Action action) { using var executionLease = _enterOwner?.Invoke(); lock(_gate) action(); }

    public ActionList(Action<string>? log = null, SourceLogSink? sourceLog = null,uint? robotId=null,Func<float>? clockSeconds=null,Func<IDisposable>? enterOwner=null)
    {
        _enterOwner=enterOwner;
        _log = log; _sourceLog = sourceLog;_watcher.RobotId=robotId;_watcher.SourceLog=sourceLog;_watcher.LegacyLog=log;_watcher.ClockSeconds=clockSeconds;
        // The watcher's D6 broadcast is the local seam; forward it to the game sink when one is set.
        _watcher.RobotCompletedActionBroadcast += r => _gameSend?.Invoke(r);
    }

    /// <summary>A7: IsEmpty loads the map count (+8) and returns count == 0; NOT the sum of current/pending.</summary>
    public bool IsEmpty => _queues.Count == 0;

    /// <summary>Test/inspection: the map's queue count.</summary>
    public int QueueCount => _queues.Count;

    /// <summary>Test/inspection: the queue keys in ascending signed order.</summary>
    public IReadOnlyList<int> QueueKeys => new List<int>(_queues.Keys);

    /// <summary>T6/T5: the watcher this list ticks.</summary>
    internal ActionWatcher Watcher => _watcher;

// fidelity: M8-015
    /// <summary>
    /// W12: move a callback into the watcher and return its integer handle. W13: registration is not gated by the
    /// external interface.
    /// </summary>
    public int RegisterActionEndedCallback(Action<RobotCompletedAction> callback)
    { using var executionLease = _enterOwner?.Invoke(); return _watcher.RegisterCallback(callback); }

    /// <summary>W12: erase the matching callback handle and return whether it was found.</summary>
    internal Action<string>? LifetimeObservedForTest;
    public bool UnregisterActionEndedCallback(int handle){using var executionLease = _enterOwner?.Invoke();LifetimeObservedForTest?.Invoke("unregister");return _watcher.UnregisterCallback(handle);}

    /// <summary>
    /// D6: the engine-to-game sink. Setting it turns the D2 game gate on (<see cref="ActionWatcher.HasExternalInterface"/>)
    /// and wires the watcher's broadcast seam; clearing it turns the gate off. Production binds the engine's
    /// queued host view and synchronous local animation hub.
    /// </summary>
    internal Action<RobotCompletedAction>? GameSend
    {
        set
        {
            _gameSend = value;
            _watcher.HasExternalInterface = value is not null;
            _watcher.AppSinkBound=value is not null;
        }
    }

    /// <summary>
    /// Q2: robot+0x2C7. The row settles the gate's ranges and its discard path, not what sets the byte; its only
    /// native writer is <c>BehaviorManager::UpdateRobotPropertiesForReaction</c> (0x005A2F54, M8-012/M10), which
    /// this stack does not build yet, so it is false in production and settable for the queue tests.
    /// </summary>
    public bool ExternalActionsDisabled { get; set; }

    /// <summary>Test/inspection: the queue at a key, or null.</summary>
    public ActionQueue? QueueAt(int key) => _queues.TryGetValue(key, out var q) ? q : null;

    /// <summary>
    /// A3 ActionList Clear 0x0053D916..0x0053D93A: the clearing re-entry flag returns immediately; otherwise set
    /// it, destroy the map nodes/queues, reset the map/count, clear the flag. No delayed tick is needed to empty
    /// the map here.
    /// </summary>
    public void Clear()
    {
        using var executionLease = _enterOwner?.Invoke();
        lock (_gate)
        {
            if (_clearing) return;
            _clearing = true;
            LifetimeObservedForTest?.Invoke("clear");
            // fidelity: M8-024
            Behavior.SteppedBehavior.ReportMissing("M8-024 pending-survivor shipped callback census at Robot teardown remains unread; Clear mechanics do not establish reachability");
            if (_queues.Count > 1)
                Behavior.SteppedBehavior.ReportMissing("M8-015 List.Clear native left/right/node post-order 0x0053FB5C..0x0053FB82 requires checked insertion/balancing tree shape; current managed ascending cleanup remains an implementation gap");
            foreach (var q in _queues.Values) q.Dispose();
            _queues.Clear();
            _clearing = false;
        }
    }

    /// <summary>A6: the destructor Clears before the watcher is destroyed; it does not Update/drain the events.</summary>
    public void Dispose() => Clear();

    /// <summary>
    /// A8 GetCurrentAction 0x0053F7E0..0x0053F7F4: the current if non-null, else the pending front, else null.
    /// The list reads its main queue (key 0); inspection does not promote or Init the front.
    /// </summary>
    public IActionRunner? GetCurrentAction() =>
        _queues.TryGetValue(0, out var q) ? q.GetCurrentAction() : null;

    /// <summary>
    /// T5 ActionList::Update 0x0053F580..0x0053F5E6: iterate the map in signed ascending key order; update every
    /// queue even after a failure and keep the FIRST non-zero queue result; erase a queue only when its pending
    /// count is 0 and its current is null; then, after all queues (including an empty map), update the watcher.
    /// </summary>
    public int Update()
    {
        using var executionLease = _enterOwner?.Invoke();
        lock (_gate)
        {
            // fidelity: M5-063 — completion callback can queue a driving end animation here.
            int result = 0;
            int? previousKey = null;
            while (true)
            {
                // Native advances/erases AFTER Queue.Update (0x0053F5B2..0x0053F5D6).
                // E2G completion can synchronously insert a later parallel queue; visit it this tick.
                // Do not keep a SortedDictionary enumerator across that callback.
                KeyValuePair<int, ActionQueue>? next = null;
                foreach (var entry in _queues)
                    if (previousKey is null || entry.Key > previousKey.Value) { next = entry; break; }
                if (next is not { } kv) break;
                previousKey = kv.Key;
                int r = kv.Value.Update(_watcher);
                if (result == 0 && r != 0) result = r;
                if (kv.Value.Current is null && kv.Value.PendingCount == 0
                    && _queues.TryGetValue(kv.Key, out var saved) && ReferenceEquals(saved, kv.Value))
                {
                    _queues.Remove(kv.Key);
                    kv.Value.Dispose();
                }
            }
            _watcher.Update();
            while(_hostCleanup.TryDequeue(out var cleanup))cleanup();
            return result;
        }
    }

    /// <summary>
    /// The QueueAction entry (Q1..Q15). Q1: a null action or a position above 5 logs and returns API 1 without a
    /// delete. Q2: while robot+0x2C7 is set, an external-tag range (1..1,000,000 or 2,000,001..3,000,000,
    /// unsigned) or the BAD_TAG 0x03000006 is discarded through the delete path and returns API 0. Q3: the A9
    /// guard, then dispatch; the incoming +8 retries is assigned the byte argument (Q6/Q10/Q11/Q14).
    /// </summary>
    public int QueueAction(QueueActionPosition position, IActionRunner? incoming, byte retries = 0)
    {
        if(incoming is ActionRunner runner)runner.MarkQueueAdmission();
        using var executionLease = _enterOwner?.Invoke();
        lock (_gate)
        {
            if (incoming is null)
            {
                SourceLogDispatch.GetSink(_sourceLog, _log)?.Invoke(EngineLogLevel.Error, null, "ActionList.QueueAction.NullAction", "Can't queue null action");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                return 1;
            }
            // The owning robot/watcher exists even on the front-end discard paths.
            if(incoming is ActionRunner owner)owner.Watcher=_watcher;
            if ((ExternalActionsDisabled && IsExternalTag(incoming.Tag)) || incoming.State == EngineActionResult.BadTag)
            {
                string? type=RobotActionType.NameOf(incoming.Type);
                if(type is null)Behavior.SteppedBehavior.ReportMissing("M8-015 QueueAction diagnostic has no recovered RobotActionType name; system printf NULL spelling is not assumed");
                if(ExternalActionsDisabled && type is not null)
                    SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Info,"Unnamed","ActionQueue.QueueAction.ExternalActionsDisabled",$"Ignoring {type} action while external actions are disabled");
                else if(!ExternalActionsDisabled)
                {
                    if(type is not null)SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Error,null,"ActionQueue.QueueAction.ActionHasBadTag",$"Failed to set tag, deleting action {type}");
                    Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                }
                Discard(incoming);return 0;
            }
            if((byte)position>5)return InvalidPosition();
            if (DuplicateOrClearingGuard(incoming)) return 1;
            // fidelity: M8-015
            // W7/W8/W9: the engine reaches the watcher for every runner through the robot (IActionRunner::
            // GetRobotCompletedActionMessage 0x540AC2 [r0,#4] -> robot+0x250 ActionList -> [r0,#0x10] watcher).
            // QueueAction is the point the runner is accepted; a compound's setter propagates to its children.
            if (incoming is ActionRunner ar) ar.Watcher = _watcher;

            switch (position)
            {
                case QueueActionPosition.Now:
                    return QueueNowMain(incoming,retries);
                case QueueActionPosition.NowAndClearRemaining:
                    // Q9: A9 guard, then Cancel(type=-1) across ALL queues, then QueueNext on key 0.
                    Cancel(-1);
                    return QueueNextMain(incoming,retries);
                case QueueActionPosition.NowAndResume:
                    // Q13: no current and no pending -> QueueAtEnd; no current but pending -> QueueNow; current -> Q14.
                    return QueueAtFrontMain(incoming,retries);
                case QueueActionPosition.Next:
                    return QueueNextMain(incoming,retries);
                case QueueActionPosition.AtEnd:
                    return QueueAtEndMain(incoming,retries);
                case QueueActionPosition.InParallel:
                    return AddConcurrent(incoming,retries);
                default:
                    return InvalidPosition();
            }
        }
    }

    private static int InvalidPosition()
    {
        Behavior.SteppedBehavior.ReportMissing("M8-015 invalid-position Error 0x0053DB2C..0x0053DB70 exact key/format/EnumToString contract needs an expanded checked row; old fabricated BadPosition Warning removed");
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return 1;
    }

    /// <summary>C6 ActionList::Cancel(type) 0x0053DE10..0x0053DE5E: clearing returns true; otherwise OR the queues' results; no erase/tick.</summary>
    public bool Cancel(int type)
    {
        using var executionLease = _enterOwner?.Invoke();
        lock (_gate)
        {
            if (_clearing) return true;
            return CancelQueues(q=>q.Cancel(type));
        }
    }

    /// <summary>C6 ActionList::Cancel(idTag) 0x0053E704..0x0053E83A: as the type overload, comparing current +0x60.</summary>
    public bool Cancel(uint idTag)
    {
        using var executionLease = _enterOwner?.Invoke();
        lock (_gate)
        {
            if (_clearing) return true;
            return CancelQueues(q=>q.Cancel(idTag),idTag);
        }
    }

    // C6: native map iteration remains valid when synchronous completion inserts a parallel queue.
    // As in Update, read the next key after each call; managed enumerators cannot span that callout.
    private bool CancelQueues(Func<ActionQueue,bool> cancel,uint? idTag=null)
    {
        bool any=false;int? previous=null;
        while(true)
        {
            KeyValuePair<int,ActionQueue>? next=null;
            foreach(var entry in _queues)if(previous is null || entry.Key>previous.Value){next=entry;break;}
            if(next is not {} current)return any;
            previous=current.Key;
            if(cancel(current.Value))
            {
                if(any && idTag is {} tag)SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Warning,null,"ActionList.Cancel.DuplicateTags",$"Multiple actions from multiple slots cancelled with idTag={unchecked((int)tag)}");
                any=true;
            }
        }
    }

    /// <summary>Q3: the main queue (map key 0), emplaced when missing.</summary>
    private ActionQueue MainQueue()
    {
        if (!_queues.TryGetValue(0, out var q))
        {
            q = new ActionQueue(_log,_sourceLog);
            _queues[0] = q;
        }
        return q;
    }

    private int QueueNowMain(IActionRunner incoming,byte retries)
    {
        MainQueue().QueueNow(incoming,retries);
        return 0;
    }

    private int QueueNextMain(IActionRunner incoming,byte retries)
    {
        MainQueue().QueueNext(incoming,retries);
        return 0;
    }

    private int QueueAtEndMain(IActionRunner incoming,byte retries)
    {
        MainQueue().QueueAtEnd(incoming,retries);
        return 0;
    }

    /// <summary>Q13/Q14/Q15 on the main queue.</summary>
    private int QueueAtFrontMain(IActionRunner incoming,byte retries)
    {
        var q = MainQueue();
        if (q.Current is null)
        {
            // Q13: "no current + pending -> QueueNow; neither -> QueueAtEnd."
            if (q.HasPending) q.QueueNow(incoming,retries);
            else q.QueueAtEnd(incoming,retries);
            return 0;
        }
        if (!q.Interrupt(incoming,retries))
        {
            // Q15: an Interrupt refusal logs and falls back to QueueNow.
            Behavior.SteppedBehavior.ReportMissing("M8-015 QueueAtFront refusal diagnostic0x0053E51E..0x0053E578 exact key/format needs expanded checked row; old fabricated InterruptRefused Warning removed");
            q.QueueNow(incoming,retries);
        }
        return 0;
    }

    /// <summary>Q12 IN_PARALLEL 0x0053DF2C..0x0053E076: the lowest free positive signed key from 1, a separate queue, append.</summary>
    private int AddConcurrent(IActionRunner incoming,byte retries)
    {
        int key = 1;
        while (_queues.ContainsKey(key))
        {
            if (key == int.MaxValue) return 1;      // no free slot: the AddConcurrent -1 -> API1
            key++;
        }
        var q = new ActionQueue(_log,_sourceLog);
        _queues[key] = q;
        q.QueueAtEnd(incoming,retries);
        return 0;
    }

    /// <summary>
    /// A9 duplicate/clearing guard 0x0053DCE4..0x0053DD44: while clearing, Prep the incoming and delete it, true.
    /// Otherwise scan the pending raw pointers across the queues; a duplicate warns and returns true WITHOUT
    /// deleting it. It does NOT compare each separately stored current pointer.
    /// </summary>
    private bool DuplicateOrClearingGuard(IActionRunner incoming)
    {
        if (_clearing)
        {
            incoming.Prep();
            incoming.WatcherEnding();
            return true;
        }
        foreach (var q in _queues.Values)
        {
            if (q.HasPendingReference(incoming))
            {
                if(incoming.NativeName is {} name)SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Warning,null,"ActionList.QueueAction.IsDuplicate",FormattableString.Invariant($"Attempting to queue duplicate action {name} [{unchecked((int)incoming.Tag)}]"));
                else Behavior.SteppedBehavior.ReportMissing("M8-015 duplicate Warning0x0053DD62: incoming native action name not supplied; no invented log text");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Q2: the discard path (0x0053D982 / 0x0053DA44 -> 0x0053DA9A) emplaces an empty queue at key 0 and calls
    /// ActionQueue::DeleteActionAndIter on it, so the map holds one key-0 queue afterwards.
    /// </summary>
    private void Discard(IActionRunner incoming) => MainQueue().Delete(incoming);

    /// <summary>Q2: external tags are 1..1,000,000 and 2,000,001..3,000,000, unsigned.</summary>
    private static bool IsExternalTag(uint tag) =>
        (tag >= 0x00000001u && tag <= 0x000F4240u) || (tag >= 0x001E8481u && tag <= 0x002DC6C0u);
}
