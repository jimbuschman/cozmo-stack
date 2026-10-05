namespace Cozmo.Robot;

/// <summary>
/// The engine's <c>ActionQueue</c> (20261004-actionlist-extraction.md rows A2/A4/A5 and T6/T7/T8, C4/C5): one
/// current runner plus a doubly-linked pending list, a per-queue deletion-tag set and a clearing re-entry flag.
/// Native offsets are identification: current +0, pending sentinel +4, front +8, count +0xC, deletion set +0x10,
/// clearing +0x1C.
/// </summary>
internal sealed class ActionQueue : IDisposable
{
    private IActionRunner? _current;
    private readonly LinkedList<IActionRunner> _pending = new();
    private readonly HashSet<uint> _deletionTags = new();
    private bool _clearing;

    /// <summary>current +0.</summary>
    public IActionRunner? Current => _current;
    /// <summary>count +0xC.</summary>
    public int PendingCount => _pending.Count;
    public bool HasPending => _pending.Count > 0;
    /// <summary>front +8.</summary>
    public IActionRunner? Front => _pending.First?.Value;

    /// <summary>Test/inspection view of the pending list, front to back.</summary>
    public IReadOnlyList<IActionRunner> Pending
    {
        get
        {
            var list = new List<IActionRunner>(_pending.Count);
            for (var n = _pending.First; n is not null; n = n.Next) list.Add(n.Value);
            return list;
        }
    }

    /// <summary>
    /// A4 Queue Clear 0x0053F96A..0x0053F9AC: re-entry returns; set clearing; Cancel the current (F97C) then
    /// Delete it (F98A); delete the pending front-to-back WITHOUT Cancel (F998..F9A8); clear. Pending unstarted
    /// results stay NOT_STARTED.
    /// </summary>
    public void Clear()
    {
        if (_clearing) return;
        _clearing = true;
        if (_current is { } c)
        {
            c.Cancel();
            DeleteRunner(c);
            _current = null;
        }
        while (_pending.First is { } node)
        {
            DeleteRunner(node.Value);
            _pending.RemoveFirst();
        }
        _clearing = false;
    }

    /// <summary>A5: the destructor is Clear, then the deletion set, then the pending list.</summary>
    public void Dispose() => Clear();

    /// <summary>
    /// A8 GetCurrentAction 0x0053F7E0..0x0053F7F4: the current if non-null, else the pending front if any, else
    /// null. Inspection does not promote or Init the front.
    /// </summary>
    public IActionRunner? GetCurrentAction() => _current ?? _pending.First?.Value;

    /// <summary>
    /// T6/T7/T8 (0x0053F5F4..0x0053F708): promote the pending front when the current is null; the watcher's
    /// ParentActionUpdating runs BEFORE the runner's Update; one runner update per call; an exact RUNNING retains
    /// the current and returns 0; any other result Deletes the current and returns 0 iff the full result is
    /// SUCCESS or CANCELLED, else 1. No next action starts after a terminal current is deleted this tick.
    /// </summary>
    public int Update(ActionWatcher watcher)
    {
        if (_current is null)
        {
            if (_pending.First is not { } front) return 0;   // empty + null returns 0
            _current = front.Value;
            _pending.RemoveFirst();
        }
        watcher.ParentActionUpdating(_current);
        uint result = _current.Update();
        if (result == EngineActionResult.Running) return 0;
        DeleteRunner(_current);
        _current = null;
        return result == EngineActionResult.Success || result == EngineActionResult.Cancelled ? 0 : 1;
    }

    /// <summary>
    /// Q5..Q8 QueueNow 0x0053E24C..0x0053E36E. Pending non-empty: Delete the current WITHOUT Cancel (null allowed)
    /// and prepend the incoming before the pending front. Pending empty: Cancel the current first (if non-null),
    /// then Delete it, then QueueAtEnd. No synchronous Init/Update.
    /// </summary>
    public void QueueNow(IActionRunner incoming)
    {
        if (_pending.Count > 0)
        {
            if (_current is { } c)
            {
                DeleteRunner(c);                 // Q6: WITHOUT Cancel; the old state can remain RUNNING
                _current = null;
            }
            _pending.AddFirst(incoming);
        }
        else
        {
            if (_current is { } c)
            {
                c.Cancel();                      // Q7: Cancel (0x0053E350) BEFORE Delete (0x0053E35E)
                DeleteRunner(c);
                _current = null;
            }
            _pending.AddLast(incoming);          // QueueAtEnd 0x0053E36E
        }
    }

    /// <summary>Q11 AT_END 0x0053E16C..0x0053E248: append at the tail.</summary>
    public void QueueAtEnd(IActionRunner incoming) => _pending.AddLast(incoming);

    /// <summary>
    /// Q10 NEXT 0x0053E078..0x0053E168: pending count 0 appends; otherwise insert BEFORE the SECOND pending node
    /// (current R, pending [A,B] -> R, [A,new,B]).
    /// </summary>
    public void QueueNext(IActionRunner incoming)
    {
        if (_pending.Count == 0)
        {
            _pending.AddLast(incoming);
            return;
        }
        var second = _pending.First!.Next;
        if (second is null) _pending.AddLast(incoming);
        else _pending.AddBefore(second, incoming);
    }

    /// <summary>
    /// Q14 QueueAtFront 0x0053E49A..0x0053E51E: interrupt the current. The +0x14 predicate must return true (the
    /// caller handles the Q15 fallback). If +0x56 is false and the state is RUNNING, unlock; Reset(false); state
    /// 0x03000009; pending becomes [new, old, residual] and the current is null. The old action is not destroyed.
    /// </summary>
    public bool Interrupt(IActionRunner incoming)
    {
        if (_current is null) return false;
        if (!_current.CanInterrupt()) return false;
        if (!_current.SuppressTrackLocking && _current.State == EngineActionResult.Running) _current.UnlockTracks();
        _current.Reset(false);
        _current.State = EngineActionResult.Interrupted;
        _pending.AddFirst(_current);
        _pending.AddFirst(incoming);
        _current = null;
        return true;
    }

    /// <summary>A9: the duplicate scan looks at the pending raw pointers only, never the separately stored current.</summary>
    public bool HasPendingReference(IActionRunner runner)
    {
        for (var n = _pending.First; n is not null; n = n.Next)
            if (ReferenceEquals(n.Value, runner)) return true;
        return false;
    }

    /// <summary>
    /// C4 Queue Cancel(type) 0x0053E690..0x0053E702: type -1 is the wildcard; the current matches on +0x44 and is
    /// Cancelled then Deleted; pending matches are Deleted WITHOUT Cancel, front to back; a failed pending delete
    /// stops the loop; returns whether anything was deleted.
    /// </summary>
    public bool Cancel(int type)
    {
        bool any = false;
        if (_current is { } c && (type == -1 || c.Type == type))
        {
            c.Cancel();
            DeleteRunner(c);
            _current = null;
            any = true;
        }
        for (var node = _pending.First; node is not null; )
        {
            var next = node.Next;
            if (type == -1 || node.Value.Type == type)
            {
                if (!DeleteRunner(node.Value)) break;
                _pending.Remove(node);
                any = true;
            }
            node = next;
        }
        return any;
    }

    /// <summary>
    /// C5 Queue Cancel(unsigned idTag) 0x0053E83C..0x0053E96E: compares the current's +0x60 (Tag), not the
    /// original +0x5C; the current is Cancelled then Deleted; pending matches are Deleted only; returns whether
    /// anything was deleted.
    /// </summary>
    public bool Cancel(uint idTag)
    {
        bool any = false;
        if (_current is { } c && c.Tag == idTag)
        {
            c.Cancel();
            DeleteRunner(c);
            _current = null;
            any = true;
        }
        for (var node = _pending.First; node is not null; )
        {
            var next = node.Next;
            if (node.Value.Tag == idTag)
            {
                if (!DeleteRunner(node.Value)) break;
                _pending.Remove(node);
                any = true;
            }
            node = next;
        }
        return any;
    }

    /// <summary>
    /// D1/D2/D5: the queue's DeleteActionAndIter path. Q2's discard emplaces the key-0 queue and calls this on the
    /// action that never entered the pending list.
    /// </summary>
    public bool Delete(IActionRunner runner) => DeleteRunner(runner);

    /// <summary>
    /// D1/D2/D5: the queue deletion path. D1 inserts tag+60 into the queue deletion set (a duplicate returns
    /// false); D2 Preps before the snapshot/destructor; D5/W10 queues the watcher destruction event. D6/D7 (the
    /// game broadcast and the erase-after-broadcast) are batch 4, so the guard is released after the event.
    /// </summary>
    private bool DeleteRunner(IActionRunner runner)
    {
        if (!_deletionTags.Add(runner.Tag)) return false;
        runner.Prep();
        runner.WatcherEnding();
        _deletionTags.Remove(runner.Tag);
        return true;
    }
}
