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
    private bool _clearing;

    public ActionList(Action<string>? log = null) => _log = log;

    /// <summary>A7: IsEmpty loads the map count (+8) and returns count == 0; NOT the sum of current/pending.</summary>
    public bool IsEmpty => _queues.Count == 0;

    /// <summary>Test/inspection: the map's queue count.</summary>
    public int QueueCount => _queues.Count;

    /// <summary>Test/inspection: the queue keys in ascending signed order.</summary>
    public IReadOnlyList<int> QueueKeys => new List<int>(_queues.Keys);

    /// <summary>T6/T5: the watcher this list ticks.</summary>
    internal ActionWatcher Watcher => _watcher;

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
        if (_clearing) return;
        _clearing = true;
        foreach (var q in _queues.Values) q.Dispose();
        _queues.Clear();
        _clearing = false;
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
        int result = 0;
        var erase = new List<int>();
        foreach (var kv in _queues)
        {
            int r = kv.Value.Update(_watcher);
            if (result == 0 && r != 0) result = r;
            if (kv.Value.Current is null && kv.Value.PendingCount == 0) erase.Add(kv.Key);
        }
        foreach (int key in erase)
        {
            if (_queues.TryGetValue(key, out var q))
            {
                _queues.Remove(key);
                q.Dispose();
            }
        }
        _watcher.Update();
        return result;
    }

    /// <summary>
    /// The QueueAction entry (Q1..Q15). Q1: a null action or a position above 5 logs and returns API 1 without a
    /// delete. Q2: while robot+0x2C7 is set, an external-tag range (1..1,000,000 or 2,000,001..3,000,000,
    /// unsigned) or the BAD_TAG 0x03000006 is discarded through the delete path and returns API 0. Q3: the A9
    /// guard, then dispatch; the incoming +8 retries is assigned the byte argument (Q6/Q10/Q11/Q14).
    /// </summary>
    public int QueueAction(QueueActionPosition position, IActionRunner? incoming, byte retries = 0)
    {
        if (incoming is null)
        {
            _log?.Invoke("warning: ActionList.QueueAction.NullAction");
            return 1;
        }
        if ((int)position > (int)QueueActionPosition.InParallel)
        {
            _log?.Invoke("warning: ActionList.QueueAction.BadPosition");
            return 1;
        }
        // Q2: the disabled gate (robot+0x2C7) tests the incoming +0x60 Tag against the external ranges. The
        // BAD_TAG 0x03000006 is the action's STATE +0x18 and is checked outside the gate (the native cbz at
        // 0x0053D972 runs the state compare with the byte zero).
        if ((ExternalActionsDisabled && IsExternalTag(incoming.Tag)) || incoming.State == EngineActionResult.BadTag)
        {
            Discard(incoming);
            return 0;
        }
        if (DuplicateOrClearingGuard(incoming)) return 1;
        incoming.RetriesRemain = retries;

        switch (position)
        {
            case QueueActionPosition.Now:
                return QueueNowMain(incoming);
            case QueueActionPosition.NowAndClearRemaining:
                // Q9: A9 guard, then Cancel(type=-1) across ALL queues, then QueueNext on key 0.
                Cancel(-1);
                return QueueNextMain(incoming);
            case QueueActionPosition.NowAndResume:
                // Q13: no current and no pending -> QueueAtEnd; no current but pending -> QueueNow; current -> Q14.
                return QueueAtFrontMain(incoming);
            case QueueActionPosition.Next:
                return QueueNextMain(incoming);
            case QueueActionPosition.AtEnd:
                return QueueAtEndMain(incoming);
            case QueueActionPosition.InParallel:
                return AddConcurrent(incoming);
            default:
                _log?.Invoke("warning: ActionList.QueueAction.BadPosition");
                return 1;
        }
    }

    /// <summary>C6 ActionList::Cancel(type) 0x0053DE10..0x0053DE5E: clearing returns true; otherwise OR the queues' results; no erase/tick.</summary>
    public bool Cancel(int type)
    {
        if (_clearing) return true;
        bool any = false;
        foreach (var q in _queues.Values) if (q.Cancel(type)) any = true;
        return any;
    }

    /// <summary>C6 ActionList::Cancel(idTag) 0x0053E704..0x0053E83A: as the type overload, comparing current +0x60.</summary>
    public bool Cancel(uint idTag)
    {
        if (_clearing) return true;
        bool any = false;
        foreach (var q in _queues.Values) if (q.Cancel(idTag)) any = true;
        return any;
    }

    /// <summary>Q3: the main queue (map key 0), emplaced when missing.</summary>
    private ActionQueue MainQueue()
    {
        if (!_queues.TryGetValue(0, out var q))
        {
            q = new ActionQueue();
            _queues[0] = q;
        }
        return q;
    }

    private int QueueNowMain(IActionRunner incoming)
    {
        MainQueue().QueueNow(incoming);
        return 0;
    }

    private int QueueNextMain(IActionRunner incoming)
    {
        MainQueue().QueueNext(incoming);
        return 0;
    }

    private int QueueAtEndMain(IActionRunner incoming)
    {
        MainQueue().QueueAtEnd(incoming);
        return 0;
    }

    /// <summary>Q13/Q14/Q15 on the main queue.</summary>
    private int QueueAtFrontMain(IActionRunner incoming)
    {
        var q = MainQueue();
        if (q.Current is null)
        {
            // Q13: "no current + pending -> QueueNow; neither -> QueueAtEnd."
            if (q.HasPending) q.QueueNow(incoming);
            else q.QueueAtEnd(incoming);
            return 0;
        }
        if (!q.Interrupt(incoming))
        {
            // Q15: an Interrupt refusal logs and falls back to QueueNow.
            _log?.Invoke("warning: ActionList.QueueAction.InterruptRefused");
            q.QueueNow(incoming);
        }
        return 0;
    }

    /// <summary>Q12 IN_PARALLEL 0x0053DF2C..0x0053E076: the lowest free positive signed key from 1, a separate queue, append.</summary>
    private int AddConcurrent(IActionRunner incoming)
    {
        int key = 1;
        while (_queues.ContainsKey(key))
        {
            if (key == int.MaxValue) return 1;      // no free slot: the AddConcurrent -1 -> API1
            key++;
        }
        var q = new ActionQueue();
        _queues[key] = q;
        q.QueueAtEnd(incoming);
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
                _log?.Invoke("warning: ActionList.QueueAction.Duplicate");
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
