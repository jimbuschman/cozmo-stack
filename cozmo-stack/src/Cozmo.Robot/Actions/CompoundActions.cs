namespace Cozmo.Robot;

// fidelity: M10-008, M13-028
/// <summary>
/// The engine's <c>ICompoundAction</c> base (20261004-actionlist-extraction.md rows P1-P8): an
/// <see cref="ActionRunner"/> whose runner type is 0xFFFFFFFE with mask 0, holding a child list in insertion
/// order, an ignore-failure predicate map keyed by the child pointer, a completion-union/type cache keyed by the
/// completed child's current tag, a proxy tag/flag, and the DeleteOnCompletion flag.
///
/// Native offsets are identification, not proposed C# layout: child list +0x70, completion cache +0x7C,
/// predicate map +0x88, proxy tag +0x94, proxy flag +0x98, delete-on-completion +0x99. The shared
/// <c>ICompoundAction::Reset(bool)</c> is 0x0054EC56 and <c>ClearActions()</c> is 0x0054EFA4.
///
/// This class carries the shared mechanisms only; the child tick lives in
/// <see cref="CompoundActionSequential"/> (S1-S9) and <see cref="CompoundActionParallel"/> (R1-R6).
/// </summary>
public abstract class CompoundAction : ActionRunner
{
    /// <summary>P1: the runner type every compound ctor uses, 0xFFFFFFFE.</summary>
    internal const int CompoundRunnerType = unchecked((int)0xFFFFFFFE);

    private readonly List<ActionRunner> _children = new();                       // +0x70
    private readonly Dictionary<ActionRunner, Func<uint, ActionRunner, bool>> _ignorePredicates = new();  // +0x88
    private readonly Dictionary<uint, (uint Union, uint Type)> _completionCache = new();                  // +0x7C
    private readonly Func<float>? _engineClock;
    private bool _proxy;                                                          // +0x98
    private uint _proxyTag;                                                       // +0x94
    private bool _deleteOnCompletion = true;                                      // +0x99

    /// <summary>
    /// P1: the common ctor. The child list order is preserved; a null child warns and is skipped; the
    /// completion cache and predicate map start empty; the proxy flag is false and DeleteOnCompletion is true.
    /// The ctor installs no ignore predicate on any child (P1: "constructor bools install no ignore predicate").
    /// </summary>
    protected CompoundAction(Func<float>? engineClock, IEnumerable<ActionRunner?>? children)
        : base(CompoundRunnerType, 0)
    {
        _engineClock = engineClock;
        if (children is null) return;
        foreach (var child in children) AppendChild(child);
    }

    /// <summary>Test/inspection: the live child list in insertion order.</summary>
    public IReadOnlyList<ActionRunner> Children => _children;

    /// <summary>P5: +0x99, default true.</summary>
    public bool DeleteOnCompletion => _deleteOnCompletion;

    // L7: the compound's engine clock (BaseStationTimer::GetCurrentTimeInSeconds).
    protected override float EngineClockSeconds => _engineClock?.Invoke() ?? 0f;

    // fidelity: M7-020
    // W7/W8/W9: a compound reaches the watcher through the robot like every runner (ActionList.QueueAction sets it);
    // setting it propagates to the children so each child's ActionStartUpdating/ActionEndUpdating/ActionEnding fires
    // under the compound's root. Children added after the watcher is set get it in AddAction/AppendChild.
    internal override ActionWatcher? Watcher
    {
        get => base.Watcher;
        set
        {
            base.Watcher = value;
            foreach (var child in _children) child.Watcher = value;
        }
    }

    /// <summary>P3: append a child, copying the parent logging +0x57; the base body does not use the extra bool.</summary>
    protected virtual void AddAction(ActionRunner child, bool other)
    {
        _ = other;                                        // P3: forwarded, unused by the base body
        child.Log = Log;                                  // +0x57
        child.Watcher = Watcher;
        _children.Add(child);
    }

    /// <summary>
    /// P2: the raw bool overload. The first bool is ignoreFailure: true installs an always-true predicate keyed
    /// by the child pointer, false leaves the predicate map empty for that child. The second bool is forwarded
    /// to the virtual <see cref="AddAction(ActionRunner, bool)"/>, not treated as the ignore bool.
    /// </summary>
    public void AddAction(ActionRunner? child, bool ignoreFailure, bool other)
    {
        if (child is null)
        {
            Log?.Invoke("warning: ICompoundAction.AddAction.NullChild");
            return;
        }
        if (ignoreFailure) _ignorePredicates[child] = static (_, _) => true;
        else _ignorePredicates.Remove(child);
        AddAction(child, other);
    }

    /// <summary>P1: append an initial child without an ignore predicate.</summary>
    private void AppendChild(ActionRunner? child)
    {
        if (child is null)
        {
            Log?.Invoke("warning: ICompoundAction.Constructor.NullChild");
            return;
        }
        child.Log = Log;
        child.Watcher = Watcher;
        _children.Add(child);
    }

    /// <summary>P4: a missing predicate is false; otherwise invoke the predicate with (full result, child pointer).</summary>
    protected virtual bool ShouldIgnoreFailure(ActionRunner child, uint fullResult) =>
        _ignorePredicates.TryGetValue(child, out var predicate) && predicate(fullResult, child);

    /// <summary>P5: set +0x99 and propagate to compound children in child order.</summary>
    public void SetDeleteOnCompletion(bool value)
    {
        _deleteOnCompletion = value;
        foreach (var child in _children)
            if (child is CompoundAction compound) compound.SetDeleteOnCompletion(value);
    }

    /// <summary>
    /// P8 SetProxyTag 0x0054F20C: set the proxy tag (+0x94) and flag (+0x98), then write the compound's own
    /// type (+0x44) from the first live child whose current tag matches, and finally from the completion cache
    /// (+0x7C) when the tag is found there.
    /// </summary>
    protected void SetCompletionUnionProxy(uint childTag)
    {
        _proxyTag = childTag;
        _proxy = true;
        foreach (var child in _children)
            if (child.Tag == childTag) Type = child.Type;              // native scans all children; the last match wins
        if (_completionCache.TryGetValue(childTag, out var cached)) Type = (int)cached.Type;
    }

    /// <summary>
    /// P6 StoreUnionAndDelete 0x0054F050: read the virtual child union BEFORE Prep; cache union/type by the
    /// child's current tag; Prep the child; a proxy match copies the child's type to the compound's +0x44. With
    /// DeleteOnCompletion true, erase the child (its destruction runs here, the last reference) and return true.
    /// With it false, unlock the child unless +0x56 or NOT_STARTED, retain it in the list and return false. No
    /// Cancel/Stop/state change.
    /// </summary>
    protected bool StoreCompletionUnionAndDelete(ActionRunner child)
    {
        uint union = child.GetCompletionUnion();                       // virtual child union BEFORE Prep
        _completionCache[child.Tag] = (union, (uint)child.Type);       // cache by current tag
        child.Prep();
        if (_proxy && child.Tag == _proxyTag) Type = child.Type;       // P6: proxy match copies the child type
        if (_deleteOnCompletion)
        {
            _children.Remove(child);
            child.WatcherEnding();                                     // destructor only on the last reference
            return true;
        }
        if (!child.SuppressTrackLocking && child.State != EngineActionResult.NotStarted)
            child.UnlockTracks();                                      // P6: false unlock unless +0x56 or NOT_STARTED
        return false;                                                  // retained in the list
    }

    /// <summary>
    /// P8: the proxy completion union. Without the proxy, the base +0x1C cache. With it, the first live child
    /// whose current tag matches, then the cache by tag, else warn and fall back to the base. 0x0054F270 does
    /// NOT write the compound's +0x44: only <see cref="SetCompletionUnionProxy"/> and
    /// <see cref="StoreCompletionUnionAndDelete"/> do.
    /// </summary>
    public override uint GetCompletionUnion()
    {
        if (!_proxy) return base.GetCompletionUnion();
        foreach (var child in _children)
        {
            if (child.Tag != _proxyTag) continue;
            return child.GetCompletionUnion();
        }
        if (_completionCache.TryGetValue(_proxyTag, out var cached)) return cached.Union;
        Log?.Invoke("warning: ICompoundAction.GetCompletionUnion.ProxyTagMissing");
        return base.GetCompletionUnion();
    }

    /// <summary>
    /// P7 DeleteActions: destroy the remaining children in insertion order. A retained child (DeleteOnCompletion
    /// false) that is non-NOT_STARTED and non-suppressed is re-taken with the unsigned-owner ownership test before
    /// its destruction. Each child is Prepped then destroyed; the predicates, cache and list are cleared. No
    /// generic child Cancel.
    /// </summary>
    protected void DeleteActions()
    {
        foreach (var child in _children.ToArray())
        {
            if (!_deleteOnCompletion && child.State != EngineActionResult.NotStarted && !child.SuppressTrackLocking)
            {
                if (!child.OwnsTracksByUnsignedOwner()) child.RetakeTracks();
            }
            child.Prep();
            child.WatcherEnding();
        }
        _children.Clear();
        _ignorePredicates.Clear();
        _completionCache.Clear();
    }

    /// <summary>P7: the compound destructor tail destroys its children before the base stop/unlock/watch (R6).</summary>
    public override void WatcherEnding()
    {
        DeleteActions();
        base.WatcherEnding();
    }

    /// <summary>
    /// 0x0054EFA4 ClearActions: DeleteActions, clear the child list, destroy the predicate tree, then tail-call
    /// Reset(true).
    /// </summary>
    public void ClearActions()
    {
        DeleteActions();
        _children.Clear();
        _ignorePredicates.Clear();
        Reset(true);
    }

    /// <summary>
    /// ICompoundAction::Reset(bool) 0x0054EC56: state = NOT_STARTED, then reset each child front to back with the
    /// same bool. This is the vtable slot R4's retry calls; the derived tick state is reset by the override.
    /// </summary>
    public override void Reset(bool unlockTracks)
    {
        State = EngineActionResult.NotStarted;
        foreach (var child in _children.ToArray()) child.Reset(unlockTracks);
    }

    // The concrete compounds drive their own child tick through UpdateInternal; these two are never reached.
    public override uint CheckIfDone() => State;

    // IActionRunner::Interrupt 0x00540250 calls virtual +0x14 and acts only on a 1. The compound vtables'
    // vptr+0x14 relocation target is 0x0052B0B2 (Thumb 0x0052B0B3), which is `movs r0,#0; bx lr`: it returns 0.
    // `_ZTVN4Anki5Cozmo24CompoundActionSequentialE` = 0x01022044 (vptr 0x0102204C, +0x14 = 0x01022060 =
    // 0x0052B0B3); `_ZTVN4Anki5Cozmo22CompoundActionParallelE` = 0x01022074 (vptr 0x0102207C, +0x14 = 0x01022090
    // = 0x0052B0B3). Both return 0, so a compound refuses Q14 NOW_AND_RESUME and the queue falls back to QueueNow.
    public override bool CanInterrupt() => false;
}

// fidelity: M13-028
/// <summary>
/// CompoundActionSequential (20261004-actionlist-extraction.md S1-S9): children run one at a time in list
/// order. Native offsets: delay +0x9C, deadline +0xA0, iterator +0xA4, firstTick +0xA8.
/// </summary>
public class CompoundActionSequential : CompoundAction
{
    private float _delay;                                                          // +0x9C, ctor 0
    private float _deadline = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));   // +0xA0, ctor -1.0f
    private int _iterator;                                                         // +0xA4
    private bool _firstTick = true;                                                // +0xA8

    /// <summary>S1: NOT_STARTED, delay 0, deadline -1.0f, iterator front, firstTick true, children Reset(true).</summary>
    public CompoundActionSequential(Func<float>? engineClock = null, params ActionRunner?[] children)
        : base(engineClock, children)
    {
        foreach (var child in Children.ToArray()) child.Reset(true);
    }

    /// <summary>S2: the derived vtable+0x24 hook. The base returns 0; a nonzero result fails 0x0300001C with no child tick.</summary>
    protected virtual uint DerivedPreTick() => 0;

    /// <summary>S1/S7: the inter-child delay +0x9C. The ctor sets 0; a derived compound may set it.</summary>
    protected void SetDelay(float seconds) => _delay = seconds;

    /// <summary>S1/S6: Reset(bool) resets the iterator, firstTick and deadline (the delay is kept), then the base resets state and children.</summary>
    public override void Reset(bool unlockTracks)
    {
        base.Reset(unlockTracks);
        _deadline = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
        _iterator = 0;
        _firstTick = true;
    }

    /// <summary>
    /// S2-S6: the derived hook first; on the first tick the iterator is the actual list front and an empty list
    /// returns SUCCESS without parent RunCallbacks; copy +0x56 to the child before the deadline gate; a live
    /// deadline returns RUNNING; otherwise tick the child and dispatch on the result category.
    /// </summary>
    protected override uint UpdateInternal()
    {
        float now = EngineClockSeconds;

        if (DerivedPreTick() != 0) return 0x0300001C;                              // S2

        if (_firstTick)
        {
            _firstTick = false;
            _iterator = 0;
            if (Children.Count == 0) return EngineActionResult.Success;           // S3: no RunCallbacks
        }

        var child = Children[_iterator];
        child.SuppressTrackLocking = SuppressTrackLocking;                         // S3: copy +56 BEFORE the delay gate
        if (_deadline >= 0f && now < _deadline) return EngineActionResult.Running; // S3
        uint result = child.Update();                                              // S3

        return (result >> 24) switch                                              // S4
        {
            0 => MoveToNext(now),
            1 => EngineActionResult.Running,
            2 or 3 => Failure(result),
            4 => TakeRetry() ? Retry() : Failure(result),
            _ => EngineActionResult.Success,                                      // S4: category > 4
        };
    }

    /// <summary>S5: parent RunCallbacks(full failure) BEFORE the ignore predicate; ignored -> MoveToNext, else store/delete and return the full failure.</summary>
    private uint Failure(uint result)
    {
        var child = Children[_iterator];
        RunCompletionCallbacks(result);                                            // S5 BEFORE predicate
        if (ShouldIgnoreFailure(child, result)) return MoveToNext(EngineClockSeconds);
        StoreCompletionUnionAndDelete(child);
        return result;
    }

    /// <summary>S6: the parent retry was taken; reset the children, transient NOT_STARTED, deadline -1, iterator front, firstTick true; return RUNNING.</summary>
    private uint Retry()
    {
        foreach (var child in Children.ToArray()) child.Reset(true);
        State = EngineActionResult.NotStarted;                                     // transient; the outer Update stores RUNNING
        _deadline = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
        _iterator = 0;
        _firstTick = true;
        return EngineActionResult.Running;
    }

    /// <summary>S7: a positive delay stamps now+delay; store/delete the finished child and advance; at the end RunCallbacks(SUCCESS)/SUCCESS; a remaining live deadline is RUNNING; else S8.</summary>
    private uint MoveToNext(float now)
    {
        if (_delay > 0f) _deadline = now + _delay;                                 // S7: f32 add
        var finished = Children[_iterator];
        bool erased = StoreCompletionUnionAndDelete(finished);
        if (!erased) _iterator++;                                                  // P6 erase keeps the next at this index
        if (_iterator >= Children.Count)
        {
            RunCompletionCallbacks(EngineActionResult.Success);                    // S7
            return EngineActionResult.Success;
        }
        if (_deadline > now) return EngineActionResult.Running;                    // S7
        return ImmediateNextTick();
    }

    /// <summary>
    /// S8: copy +0x56 and tick exactly ONE next child in the same tick. RUNNING returns. A terminal result
    /// store/deletes that child; at the end RunCallbacks(full result)/full result; a remaining SUCCESS returns
    /// RUNNING; otherwise the full nonzero result is returned. No ignore/retry on this path (S9).
    /// </summary>
    private uint ImmediateNextTick()
    {
        var child = Children[_iterator];
        child.SuppressTrackLocking = SuppressTrackLocking;                         // S8
        uint result = child.Update();
        if (result == EngineActionResult.Running) return EngineActionResult.Running;
        bool erased = StoreCompletionUnionAndDelete(child);
        if (!erased) _iterator++;
        if (_iterator >= Children.Count)
        {
            RunCompletionCallbacks(result);                                        // S8: full next result
            return result;
        }
        return result == EngineActionResult.Success ? EngineActionResult.Running : result;   // S8
    }
}

// fidelity: M10-008
/// <summary>
/// CompoundActionParallel (20261004-actionlist-extraction.md R1-R6): every child is ticked in insertion order
/// on the same tick; a child failure stops the later children unless ignored or retried.
/// </summary>
public class CompoundActionParallel : CompoundAction
{
    /// <summary>R1: the common ctor; the children are ticked in insertion order.</summary>
    public CompoundActionParallel(Func<float>? engineClock = null, params ActionRunner?[] children)
        : base(engineClock, children) { }

    /// <summary>
    /// R1-R5: copy +0x56 to each child before its Update; dispatch on the result category. 0 store/delete/
    /// continue; 1 advance and remember RUNNING/continue; 2/3 (or unretried 4) RunCallbacks(full result) BEFORE
    /// the predicate, ignored -> store/delete/continue else immediate full failure; 4 retried -> take the retry,
    /// Reset(true) and RUNNING with no further children this tick. Category > 4 branches back without advancing
    /// (R5). At the end any running -> RUNNING; else RunCallbacks(SUCCESS)/SUCCESS, empty included.
    /// </summary>
    protected override uint UpdateInternal()
    {
        bool anyRunning = false;
        int i = 0;
        while (i < Children.Count)
        {
            var child = Children[i];
            child.SuppressTrackLocking = SuppressTrackLocking;                     // R1: copy +56
            uint result = child.Update();
            uint category = result >> 24;

            if (category == 0)                                                     // R2
            {
                bool erased = StoreCompletionUnionAndDelete(child);
                if (!erased) i++;
                continue;
            }
            if (category == 1)                                                     // R2
            {
                anyRunning = true;
                i++;
                continue;
            }
            if (category == 4 && TakeRetry())
            {
                Reset(true);                                                       // R4: virtual Reset(true)
                return EngineActionResult.Running;                                 // rest not updated this tick
            }
            if (category > 4)                                                      // R5: no iterator advance
            {
                continue;
            }

            // R3: category 2/3 or unretried 4. RunCallbacks(full result) BEFORE the predicate.
            RunCompletionCallbacks(result);
            if (ShouldIgnoreFailure(child, result))
            {
                bool erased = StoreCompletionUnionAndDelete(child);                // P6 Preps the child
                if (!erased) i++;
                continue;
            }
            return result;                                                         // immediate full failure
        }

        if (anyRunning) return EngineActionResult.Running;                         // R2
        RunCompletionCallbacks(EngineActionResult.Success);                        // R2: including empty
        return EngineActionResult.Success;
    }
}