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
    private readonly Action<string>? _log;
    private readonly SourceLogSink? _sourceLog;
    internal ActionQueue(Action<string>? log=null,SourceLogSink? sourceLog=null){_log=log;_sourceLog=sourceLog;}

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
            DeleteCurrent(c);
        }
        while (_pending.First is { } node)
        {
            DeletePending(node,out _);
        }
        _clearing = false;
    }

    /// <summary>A5: the destructor is Clear, then the deletion set, then the pending list.</summary>
    public void Dispose(){Clear();_deletionTags.Clear();_pending.Clear();}

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
        if (result == EngineActionResult.Running)
        {
            Behavior.SteppedBehavior.ReportMissing("M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C and CozmoContext.SetSdkStatus(type1,name)0x0053F6F2 recipients are not bound; normal queue path is not classified SDK-only");
            return 0;
        }
        Behavior.SteppedBehavior.ReportMissing("M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696 / CozmoContext.SetSdkStatus(type1,empty)0x0053F6B4 recipients before Delete are not bound");
        DeleteCurrent(_current);
        return result == EngineActionResult.Success || result == EngineActionResult.Cancelled ? 0 : 1;
    }

    /// <summary>
    /// Q5..Q8 QueueNow 0x0053E24C..0x0053E36E. Pending non-empty: Delete the current WITHOUT Cancel (null allowed)
    /// and prepend the incoming before the pending front. Pending empty: Cancel the current first (if non-null),
    /// then Delete it, then QueueAtEnd. No synchronous Init/Update.
    /// </summary>
    public void QueueNow(IActionRunner incoming,byte retries=0)
    {
        if (_pending.Count > 0)
        {
            if (_current is { } c)
            {
                if(c.NativeName is {} oldName&&incoming.NativeName is {} newName)
                    SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Debug,"Unnamed","ActionQueue.QueueNow.CancelingPrevious",FormattableString.Invariant($"Canceling {oldName} [{unchecked((int)c.Tag)}] in favor of action {newName} [{unchecked((int)incoming.Tag)}]"));
                else Behavior.SteppedBehavior.ReportMissing("M8-015 QueueNow Debug0x0053E2A6: native action names not supplied");
                DeleteCurrent(c);                // Q6: WITHOUT Cancel; the old state can remain RUNNING
            }
            incoming.RetriesRemain=retries;
            _pending.AddFirst(incoming);
        }
        else
        {
            if (_current is { } c)
            {
                c.Cancel();                      // Q7: Cancel (0x0053E350) BEFORE Delete (0x0053E35E)
                DeleteCurrent(c);
            }
            QueueAtEnd(incoming,retries);          // QueueAtEnd 0x0053E36E
        }
    }

    // Host-only admission guard for a behavior invalidated by synchronous callbacks.
    // It never inserts or completes an invalidated incoming runner.
    internal bool QueueNowGuarded(IActionRunner incoming,byte retries,Func<bool> current)
    {
        if(!current())return false;
        if(_pending.Count>0)
        {
            if(_current is {} c)
            {
                if(c.NativeName is {} oldName&&incoming.NativeName is {} newName)
                    SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Debug,"Unnamed","ActionQueue.QueueNow.CancelingPrevious",FormattableString.Invariant($"Canceling {oldName} [{unchecked((int)c.Tag)}] in favor of action {newName} [{unchecked((int)incoming.Tag)}]"));
                else Behavior.SteppedBehavior.ReportMissing("M8-015 QueueNow Debug0x0053E2A6: native action names not supplied");
                if(!current())return false;
                DeleteCurrent(c);
                if(!current())return false;
            }
            incoming.RetriesRemain=retries;
            if(incoming is ActionRunner runner)runner.MarkQueueAdmission();
            _pending.AddFirst(incoming);return true;
        }
        if(_current is {} old)
        {
            old.Cancel();if(!current())return false;
            DeleteCurrent(old);if(!current())return false;
        }
        if(incoming is ActionRunner admitted)admitted.MarkQueueAdmission();
        QueueAtEnd(incoming,retries);return true;
    }
    /// <summary>Q11 AT_END 0x0053E16C..0x0053E248: append at the tail.</summary>
    public void QueueAtEnd(IActionRunner incoming,byte retries=0){incoming.RetriesRemain=retries;_pending.AddLast(incoming);}

    /// <summary>
    /// Q10 NEXT 0x0053E078..0x0053E168: pending count 0 appends; otherwise insert BEFORE the SECOND pending node
    /// (current R, pending [A,B] -> R, [A,new,B]).
    /// </summary>
    public void QueueNext(IActionRunner incoming,byte retries=0)
    {
        incoming.RetriesRemain=retries;
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
    public bool Interrupt(IActionRunner incoming,byte retries=0)
    {
        if (_current is null) return false;
        if (!_current.CanInterrupt()) return false;
        if (!_current.SuppressTrackLocking && _current.State == EngineActionResult.Running) _current.UnlockTracks();
        _current.Reset(false);
        _current.State = EngineActionResult.Interrupted;
        Behavior.SteppedBehavior.ReportMissing("M8-015 QueueAtFront success Info0x0053E4E0 exact key/format needs expanded checked row; interrupt and byte-store/list order are built");
        incoming.RetriesRemain=retries;
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
            DeleteCurrent(c);
            any = true;
        }
        for(var node=_pending.First;node is not null;)
        {
            if(type==-1||node.Value.Type==type)
            {
                if(!DeletePending(node,out var next))break;
                any=true;node=next;
                if(node is not null&&node.List!=_pending){MissingFreedSuccessor();break;}
            }
            else node=node.Next;
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
            DeleteCurrent(c);
            any = true;
        }
        for(var node=_pending.First;node is not null;)
        {
            if(node.Value.Tag==idTag)
            {
                if(any)SourceLogDispatch.GetSink(_sourceLog,_log)?.Invoke(EngineLogLevel.Warning,null,"ActionQueue.Cancel.DuplicateIdTags",FormattableString.Invariant($"Multiple actions with tag={unchecked((int)idTag)} found in queue"));
                if(!DeletePending(node,out var next))break;
                any=true;node=next;
                if(node is not null&&node.List!=_pending){MissingFreedSuccessor();break;}
            }
            else node=node.Next;
        }
        return any;
    }

    /// <summary>
    /// D1/D2/D5: the queue's DeleteActionAndIter path. Q2's discard emplaces the key-0 queue and calls this on the
    /// action that never entered the pending list.
    /// </summary>
    public bool Delete(IActionRunner runner) => DeleteRunner(runner);

    private bool DeleteCurrent(IActionRunner runner)=>DeleteRunner(runner,()=>_current=null);
    private bool DeletePending(LinkedListNode<IActionRunner> node,out LinkedListNode<IActionRunner>? next)
    {
        LinkedListNode<IActionRunner>? successor=node;
        bool result=DeleteRunner(node.Value,()=>{successor=node.Next;_pending.Remove(node);});
        next=successor;return result;
    }
    private static void MissingFreedSuccessor()=>Behavior.SteppedBehavior.ReportMissing("M1-034/M8-015 pending Cancel successor was removed by synchronous completion; native iterator at0x0053E6E8/0x0053E6EE would read freed list storage: skip this pending traversal tail");

    // fidelity: M8-015
    /// <summary>
    /// D1..D7: the queue deletion path. D1 inserts tag+60 into the queue deletion set (a duplicate returns false);
    /// D2 Preps before the game snapshot/destructor; D3 snapshots the game record before the destructor (the
    /// watcher's <see cref="ActionWatcher.BuildCompletedAction"/> applies the D2 HasExternalInterface/INTERRUPTED
    /// gate); D5/W10 the destructor queues the watcher destruction event; D6 sends the game record AFTER the
    /// destructor; D7 erases the guard tag AFTER the broadcast.
    /// </summary>
    private bool DeleteRunner(IActionRunner runner,Action? unlink=null)
    {
        uint tag=runner.Tag;
        if (!_deletionTags.Add(tag)) return false;             // D1
        runner.Prep();                                                // D2
        // The queue's runner surface is IActionRunner; only an ActionRunner carries the watcher (design point 3).
        var watcher = (runner as ActionRunner)?.Watcher;
        var record = watcher?.BuildCompletedAction(runner);           // D3 before the destructor
        runner.WatcherEnding();                                       // D5: destructor -> ActionEnding enqueues
        unlink?.Invoke();                                            // D3: clear caller ref/unlink+advance BEFORE broadcast
        if (record is { } r) watcher!.SendGameMessage(r);             // D6 after the destructor
        _deletionTags.Remove(tag);                             // D7 after the broadcast
        return true;
    }
}
