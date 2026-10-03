namespace Cozmo.Robot.Behavior;

// fidelity: M8-011
/// <summary>
/// The engine's <c>BehaviorStatus</c> as <c>BehaviorHelperComponent</c> and <c>IHelper</c> use it: 0 Failure, 1 Running, 2 Complete.
/// <c>IHelper::LogStopEvent</c> 0x005b6850 maps +0x1c 0 to "robot.behavior_helper.failure" and 2 to "...success" (0x005b68a0..0x005b68c2);
/// <c>UpdateActiveHelper</c> 0x0056de74 sends 0 to <c>OnDelegateFailure</c> (0x0056dee8), 2 to <c>OnDelegateSuccess</c> (0x0056dedc) and tests
/// <c>cmp r0,#1</c> for Running (0x0056df20).
/// </summary>
public enum BehaviorStatus
{
    Failure = 0,
    Running = 1,
    Complete = 2,
}

// fidelity: M8-011
/// <summary>
/// The control block of a <c>shared_ptr&lt;IHelper&gt;</c>. <c>AddHelperToComponent</c> 0x0056da8a builds it with the owner count at one
/// (0x0056da9c..0x0056dac0); the engine tests <c>[ctl+4] + 1 != 0</c> (0x005bebaa/0x005bebac), which is "at least one shared owner is left".
/// <see cref="Owners"/> is that count.
/// </summary>
public sealed class HelperControl
{
    private int _owners;
    internal HelperControl(IHelper pointer) { Pointer = pointer; _owners = 1; }
    internal IHelper Pointer { get; }
    /// <summary>The number of live <see cref="HelperRef"/> handles.</summary>
    public int Owners => Volatile.Read(ref _owners);
    internal void AddOwner() => Interlocked.Increment(ref _owners);
    internal void ReleaseOwner() => Interlocked.Decrement(ref _owners);
    internal bool TryAddOwner()
    {
        // weak_ptr::lock: succeeds only while an owner is left
        int cur;
        do { cur = Volatile.Read(ref _owners); if (cur <= 0) return false; }
        while (Interlocked.CompareExchange(ref _owners, cur + 1, cur) != cur);
        return true;
    }
}

// fidelity: M8-011
/// <summary>
/// One <c>shared_ptr&lt;IHelper&gt;</c> (8 bytes: pointer, control block). A handle owns one count of its control block until
/// <see cref="Release"/>; <see cref="Copy"/> is the copy constructor. The helper stack's elements, the delegate in <c>DelegateProperties</c> and
/// the creator's own local are handles, so the control block's count is what the behaviour's weak reference (+0xc4/+0xc8) sees.
/// </summary>
public sealed class HelperRef
{
    private bool _released;
    internal HelperRef(HelperControl control) { Control = control; }
    public HelperControl Control { get; }
    public IHelper Pointer => Control.Pointer;
    /// <summary>The copy constructor: one more owner.</summary>
    public HelperRef Copy() { Control.AddOwner(); return new HelperRef(Control); }
    /// <summary>The destructor: this handle's count is given back (once).</summary>
    public void Release()
    {
        if (_released) return;
        _released = true;
        Control.ReleaseOwner();
    }
}

// fidelity: M8-011
/// <summary>
/// The behaviour's weak reference to its helper: the raw pointer at IBehavior +0xc4 and the control block at +0xc8
/// (<c>IBehavior::SmartDelegateToHelper</c> 0x005bec72/0x005bec7a). It is live while the control block has an owner.
/// </summary>
public sealed class HelperWeakRef
{
    public HelperWeakRef(HelperRef helper) { Pointer = helper.Pointer; Control = helper.Control; }
    public IHelper Pointer { get; }
    public HelperControl Control { get; }
    /// <summary><c>[ctl+4] + 1 != 0</c> (0x005bebaa..0x005bebae, 0x005bd36a..0x005bd36e).</summary>
    public bool IsLive => Control.Owners != 0;
    /// <summary><c>weak_ptr::lock()</c> (0x005bd230): a new handle while an owner is left, else null.</summary>
    public HelperRef? Lock() => Control.TryAddOwner() ? new HelperRef(Control) : null;
}

// fidelity: M8-011
/// <summary>
/// <c>BehaviorHelperFactory</c>: four bytes holding the component (<c>BehaviorHelperFactory::BehaviorHelperFactory</c> 0x005b54ec stores the
/// component pointer; <c>BehaviorHelperComponent</c>'s constructor 0x0056da56 allocates it, 0x0056da5c). Its six <c>Create*</c> methods build the
/// six concrete helper classes, whose bodies are unread (RECOVERABLE_GAP), so none is built here.
/// </summary>
public sealed class BehaviorHelperFactory
{
    internal BehaviorHelperFactory(BehaviorHelperComponent component) { Component = component; }
    public BehaviorHelperComponent Component { get; }
}

// fidelity: M8-011
/// <summary>
/// The engine's <c>BehaviorHelperComponent</c>, the 0x48-byte object at <c>AIComponent+0x10</c> (0x00569aa4..0x00569ab2): +0x00 the factory, +0x04/+0x08/+0x0c
/// the helper vector (<c>shared_ptr&lt;IHelper&gt;</c> elements; the top of the stack is the last element), +0x10 the success callback, +0x28 the failure
/// callback, +0x40 the world-origin id; +0x44 is touched by no member. The constructor 0x0056da56 writes the factory, three zero words for the vector and
/// empties both callbacks (0x0056da6a..0x0056da74) and leaves +0x40 uninitialised; a C# field has no uninitialised state, so it starts at 0 (a choice; it is
/// not observable: the first <see cref="UpdateActiveHelper"/> that could read it is the one inside the first push, and with one element on the stack a
/// differing origin only sets the flag the engine then ignores, 0x0056def0..0x0056df0c).
///
/// Production entry: <c>AIComponent::Update</c> 0x00569f24 calls <c>Update</c> after <c>SevereNeedsComponent::Update</c> (0x00569f40) and
/// <c>Robot::Update</c> 0x00513eac runs that before <c>BehaviorManager::Update</c> (0x00513ee6); the only caller of <c>DelegateToHelper</c> is
/// <c>IBehavior::SmartDelegateToHelper</c> 0x005bec2c and the only caller of <c>StopHelperWithoutCallback</c> is <c>IBehavior::StopHelperWithoutCallback</c>
/// 0x005bd2b0. Here they are <see cref="AIComponent.Update"/> (run from <c>EngineRobot.Update</c>), <see cref="SteppedBehavior"/>'s
/// <c>SmartDelegateToHelper</c> and its <c>StopHelperWithoutCallback</c>.
/// </summary>
public sealed class BehaviorHelperComponent
{
    private readonly object _gate = new();
    private readonly List<HelperRef> _stack = new();
    private readonly Func<int> _worldOriginId;
    private readonly Action<string>? _log;
    private Action<CozmoRobot>? _success;      // +0x10 (manager +0x20)
    private Action<CozmoRobot>? _failure;      // +0x28 (manager +0x38)
    private int _storedWorldOriginId;          // +0x40
    // The vector keeps its storage when it empties and a pop does not zero [begin] (0x0056dcc0..0x0056dcc8 read it with no check), so the bottom slot's last pointer survives.
    private IHelper? _bottomSlot;
    private int _depth;                        // how many times the owning thread holds _gate (only read or written while it is held)

    // The engine has no locks (one thread). This stack reaches the component from the engine tick and from behaviour threads, so state is guarded by one monitor, but the
    // monitor is NOT held while code of a behaviour runs: the completion callbacks and IHelper.Stop (which calls the behaviour's StopActing) are invoked through
    // OutsideLock, which gives the monitor up completely (all re-entrant levels) for the call and takes it back after. Helper bodies (Init, UpdateWhileActiveInternal,
    // ShouldCancelDelegates, OnDelegate*) are part of the control flow and run under it.
    private readonly struct Held : IDisposable
    {
        private readonly BehaviorHelperComponent _c;
        public Held(BehaviorHelperComponent c) { _c = c; Monitor.Enter(c._gate); c._depth++; }
        public void Dispose() { _c._depth--; Monitor.Exit(_c._gate); }
    }
    private Held Enter() => new(this);

    private void OutsideLock(Action call)
    {
        int levels = _depth;
        for (int i = 0; i < levels; i++) { _depth--; Monitor.Exit(_gate); }
        try { call(); }
        finally { for (int i = 0; i < levels; i++) { Monitor.Enter(_gate); _depth++; } }
    }

    /// <param name="worldOriginId"><c>Robot::GetWorldOriginID</c> (0x004a4c48 PLT), read by <c>UpdateActiveHelper</c> and <c>DelegateToHelper</c>.</param>
    /// <param name="log">The engine's log line sink (the engine logs through <c>sChanneledInfoF</c>; its text is this stack's).</param>
    public BehaviorHelperComponent(Func<int> worldOriginId, Action<string>? log = null)
    {
        _worldOriginId = worldOriginId;
        _log = log;
        Factory = new BehaviorHelperFactory(this);   // +0x00 (0x0056da5c)
    }

    /// <summary>+0x00.</summary>
    public BehaviorHelperFactory Factory { get; }
    /// <summary>The helper stack's element count (vector size).</summary>
    public int StackCount { get { using (Enter()) return _stack.Count; } }
    /// <summary>The helpers from the bottom to the top, for inspection.</summary>
    public IReadOnlyList<IHelper> StackHelpers { get { using (Enter()) return _stack.Select(h => h.Pointer).ToList(); } }
    /// <summary>Whether the success callback (+0x10) is stored (manager +0x20 non-null).</summary>
    public bool HasSuccessCallback { get { using (Enter()) return _success is not null; } }
    /// <summary>Whether the failure callback (+0x28) is stored (manager +0x38 non-null).</summary>
    public bool HasFailureCallback { get { using (Enter()) return _failure is not null; } }
    /// <summary>+0x40.</summary>
    public int StoredWorldOriginId { get { using (Enter()) return _storedWorldOriginId; } }

    private void Info(string id, string text) => _log?.Invoke($"info: [Behaviors] {id}: {text}");

    /// <summary>
    /// <c>AddHelperToComponent(IHelper*&amp; raw)</c> 0x0056da8a: never reads the component; builds a <c>shared_ptr&lt;IHelper&gt;</c> from <c>*raw</c>
    /// (a new control block, 0x0056da9c..0x0056dac0), writes null into <c>*raw</c> (0x0056da96) and returns the handle. It does not append to the vector.
    /// </summary>
    public HelperRef AddHelperToComponent(ref IHelper? raw)
    {
        var helper = raw ?? throw new ArgumentNullException(nameof(raw));
        raw = null;
        return new HelperRef(new HelperControl(helper));
    }

    /// <summary>
    /// <c>DelegateToHelper</c> 0x0056dad8, in the engine's order: (1) <c>ClearStackMaintenanceVars</c> (0x0056dae4); (2) the success callback (+0x10) becomes
    /// the given one (0x0056dae8..0x0056daee); (3) the failure callback (+0x28) becomes the given one (0x0056daf2..0x0056daf8); (4) a non-empty stack
    /// returns false without pushing, with both new callbacks already stored (0x0056dafc..0x0056db06); (5) otherwise a copy of the handle is pushed through
    /// <c>PushHelperOntoStackAndUpdate</c> (0x0056dbdc; the call is at 0x0056db1e), the copy released, and only then +0x40 = <c>Robot::GetWorldOriginID</c> (0x0056db2c/0x0056db30),
    /// and it returns true.
    /// </summary>
    public bool DelegateToHelper(CozmoRobot robot, HelperRef helper, Action<CozmoRobot>? onSuccess, Action<CozmoRobot>? onFailure)
    {
        using (Enter())
        {
            ClearStackMaintenanceVars();
            _success = onSuccess;
            _failure = onFailure;
            if (_stack.Count != 0) return false;
            var copy = helper.Copy();
            PushHelperOntoStackAndUpdate(robot, copy);
            copy.Release();
            _storedWorldOriginId = _worldOriginId();
            return true;
        }
    }

    /// <summary>
    /// <c>PushHelperOntoStackAndUpdate</c> 0x0056dbdc: (1) logs "HelperComponent.StartNewHelper" with the helper's name (0x0056dc06..0x0056dc0c);
    /// (2) <c>IHelper::InitializeOnStack</c> (0x0056dc34); (3) appends the handle (a copy: the pointer and the control block with one more owner,
    /// 0x0056dc38..0x0056dc5a); (4) <c>UpdateActiveHelper</c> (0x0056dc62).
    /// </summary>
    internal void PushHelperOntoStackAndUpdate(CozmoRobot robot, HelperRef helper)
    {
        using (Enter())
        {
            Info("HelperComponent.StartNewHelper", helper.Pointer.Name);
            helper.Pointer.InitializeOnStack();
            if (_stack.Count == 0) _bottomSlot = helper.Pointer;
            _stack.Add(helper.Copy());
            UpdateActiveHelper(robot);
        }
    }

    /// <summary>
    /// <c>ClearStackMaintenanceVars</c> 0x0056db4c: destroys the success callback and empties it (0x0056db50..0x0056db68), then the failure callback
    /// (0x0056db6a..0x0056db82). The stack is not touched.
    /// </summary>
    public void ClearStackMaintenanceVars()
    {
        using (Enter()) { _success = null; _failure = null; }
    }

    /// <summary><c>ClearStackLifetimeVars</c> 0x0056dcdc is <c>bx lr</c>; the call-site scan finds no caller.</summary>
    public void ClearStackLifetimeVars() { }

    /// <summary>
    /// <c>StopHelperWithoutCallback(shared_ptr&lt;IHelper&gt; const&amp;)</c> 0x0056dcc0: compares the handle's pointer with the BOTTOM element's
    /// (<c>ldr r2,[r0,#4]</c>; <c>ldr r2,[r2]</c>; <c>cmp r1,r2</c>, 0x0056dcc0..0x0056dcc8); equal: <c>ClearStackFromTopToIter</c> at the bottom and true
    /// (0x0056dcd0..0x0056dcd6), otherwise false and nothing else. There is no empty-stack guard (the engine reads the first element unconditionally): on an
    /// empty stack the compare is against the stale last-bottom pointer and the clear is a no-op. It does not call <c>ClearStackMaintenanceVars</c>: the stored callbacks stay and are not invoked.
    /// </summary>
    public bool StopHelperWithoutCallback(HelperRef helper)
    {
        using (Enter())
        {
            // 0x0056dcc0..0x0056dcc8: begin and [begin] are loaded with no emptiness check. An emptied vector keeps its storage and the pop does not zero [begin],
            // so the compare is against the last bottom helper's pointer and, equal, ClearStackFromTopToIter returns at once on the empty stack (0x0056dcfa/0x0056dcfc).
            // A vector never used would fault in the engine; no caller reaches that (only a successful delegation sets the weak reference), so it answers false here.
            var bottom = _stack.Count > 0 ? _stack[0].Pointer : _bottomSlot;
            if (bottom is null || !ReferenceEquals(helper.Pointer, bottom)) return false;
            ClearStackFromTopToIter(0);
            return true;
        }
    }

    /// <summary>
    /// <c>ClearStackFromTopToIter(iterator&amp;)</c> 0x0056dcd0: the target is the helper at the iterator (0x0056dce6); the first pass has the flag 1. Each
    /// pass: a pass count of 1000 or more is the error path (0x0056dcf0/0x0056dcf4); an empty stack returns (0x0056dcfa/0x0056dcfc); otherwise the top
    /// helper gets <c>IHelper::Stop(flag)</c> (0x0056dd0e), is popped (0x0056dd12..0x0056dd32), the flag becomes 0 (0x0056dd34) and the loop ends once the
    /// popped helper was the target (0x0056dd36/0x0056dd38). The target is stopped and popped too, and only the real top gets <c>Stop(true)</c>.
    /// </summary>
    internal void ClearStackFromTopToIter(int iteratorIndex)
    {
        using (Enter())
        {
            if (iteratorIndex >= _stack.Count) return;   // an iterator past the vector: nothing to stop (the engine never builds one)
            var target = _stack[iteratorIndex].Pointer;
            bool first = true;
            for (int pass = 0; ; pass++)
            {
                if (pass >= 1000)
                {
                    // 0x0056dd3c: sErrorF line 0x106 of engine/aiComponent/behaviorHelperComponent.cpp, then sDebugBreakOnError when the debug byte is set.
                    // The message text at 0x0056dd4c..0x0056dd50 was not printed and what follows the break is not in the inventory (MISSING).
                    _log?.Invoke("error: [behaviorHelperComponent.cpp:262] ClearStackFromTopToIter exceeded 1000 passes");
                    SteppedBehavior.ReportMissing("BehaviorHelperComponent::ClearStackFromTopToIter 0x0056dd3c..0x0056dd90: the error message text printed at 0x0056dd4c..0x0056dd50 and what follows sDebugBreakOnError are not in the inventory; this stack logs the line number and returns");
                    return;
                }
                if (_stack.Count == 0) return;
                var top = _stack[^1];
                bool firstPass = first;
                OutsideLock(() => top.Pointer.Stop(firstPass));
                _stack.Remove(top);
                top.Release();
                first = false;
                if (ReferenceEquals(top.Pointer, target)) return;
            }
        }
    }

    /// <summary>
    /// <c>CheckInactiveStackHelpers(Robot const&amp;)</c> 0x0056de26 (the label of the vtable-slot loop is at 0x0056de36): an empty stack returns
    /// (0x0056de42); otherwise for every element except the top (the end-8 bound is taken once, 0x0056de46/0x0056de4c) the helper's
    /// <c>ShouldCancelDelegates</c> (vtable +0x28, 0x0056de56..0x0056de5a) is asked, and a result of exactly 1 (0x0056de5c) calls
    /// <c>ClearStackFromTopToIter</c> with the iterator at the NEXT element (0x0056de64..0x0056de66), which stops and pops everything above the cancelling
    /// helper, then returns; the cancelling helper stays.
    /// </summary>
    internal void CheckInactiveStackHelpers(CozmoRobot robot)
    {
        using (Enter())
        {
            if (_stack.Count == 0) return;
            int endMinusOne = _stack.Count - 1;
            for (int i = 0; i != endMinusOne;)
            {
                var helper = _stack[i].Pointer;
                i++;
                if (helper.ShouldCancelDelegates(robot))
                {
                    ClearStackFromTopToIter(i);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// <c>Update(Robot&amp;)</c> 0x0056de20: <c>CheckInactiveStackHelpers</c> (0x0056de26) then a tail call of <c>UpdateActiveHelper</c> (0x0056de32
    /// <c>b.w 0x008cb75c</c>). That target is a linker veneer, not code of its own: <c>bx pc</c> at 0x008cb75c, then ARM <c>ldr ip,[pc]; add pc,ip,pc</c>
    /// (0x008cb760/0x008cb764) with the word 0xffbe169c at 0x008cb768, which lands on PLT stub 0x004ace08, the import stub of <c>UpdateActiveHelper</c>
    /// (inventory Correction A5). There is no gate of its own.
    /// </summary>
    public void Update(CozmoRobot robot)
    {
        using (Enter())
        {
            CheckInactiveStackHelpers(robot);
            UpdateActiveHelper(robot);
        }
    }

    /// <summary>
    /// <c>UpdateActiveHelper(Robot&amp;)</c> 0x0056de74, branch by branch (inventory Correction A5, extraction Part 1 section 1.3):
    /// <list type="number">
    /// <item>An empty stack returns at once, reading no origin (0x0056de88..0x0056de8e).</item>
    /// <item>Origin check: if <c>GetWorldOriginID</c> differs from +0x40 the "origin changed" flag is set and +0x40 takes a second read of it
    ///   (0x0056de94..0x0056deae).</item>
    /// <item>The top helper is first: the status carried between helpers starts at 1 (0x0056dec0).</item>
    /// <item>A carried 0 calls the top's <c>OnDelegateFailure</c>, a carried 2 its <c>OnDelegateSuccess</c>, a carried 1 neither (0x0056deca..0x0056deec).</item>
    /// <item>With the origin flag set and more than one element, the top is stopped with status 0 whatever those calls returned; with exactly one
    ///   element it continues (0x0056def0..0x0056df0c).</item>
    /// <item>A carried status other than 1 stops the top with that status (0x0056df0e..0x0056dfc2).</item>
    /// <item>Otherwise <c>UpdateWhileActive</c> runs (0x0056df1c): a status other than 1 stops the top with it; Running with no sub-helper ends the loop; Running with a
    ///   sub-helper logs "HelperComponent.UpdateActive.SubHelper" and pushes it with <c>PushHelperOntoStackAndUpdate</c> (0x0056df5e..0x0056dfac), then the loop
    ///   runs again on the new top in the same tick (0x0056dfb8..0x0056dfbe).</item>
    /// <item>The stop step logs "HelperComponent.UpdateActive.Complete", calls <c>IHelper::Stop(true)</c> and pops the top (0x0056dfe2..0x0056e066); if the stack is not
    ///   empty it logs "HelperComponent.UpdateActive.ReturningToPrevious" and the status of the stopped helper is carried to the new top, with the
    ///   origin flag carried as "flag and size != 1" (0x0056e074..0x0056e0e4).</item>
    /// <item>When the loop ends with the stack empty and a carried status other than 1, the completion callback is chosen (0 the failure callback, 2 the success
    ///   callback, none for anything else), both stored callbacks are cleared, and then the chosen one is called with the robot (0x0056e0fa..0x0056e15a).</item>
    /// </list>
    /// </summary>
    internal void UpdateActiveHelper(CozmoRobot robot)
    {
        using (Enter())
        {
            if (_stack.Count == 0) return;                                   // 0x0056de8e
            int current = _worldOriginId();                                  // 0x0056de94
            bool originChanged;
            if (current != _storedWorldOriginId)                             // 0x0056de9a
            {
                originChanged = true;                                        // 0x0056dea4
                _storedWorldOriginId = _worldOriginId();                     // second read, 0x0056deae
            }
            else originChanged = false;                                      // 0x0056de9e
            if (_stack.Count == 0) return;                                   // 0x0056deb6
            int carried = 1;                                                 // 0x0056dec0

            while (_stack.Count != 0)                                        // loop test 0x0056e0f2; an empty stack ends it (3.10)
            {
                var top = _stack[^1].Pointer;
                if (carried == 0) carried = (int)top.OnDelegateFailure(robot);       // 0x0056dee2..0x0056deec
                else if (carried == 2) carried = (int)top.OnDelegateSuccess(robot);  // 0x0056ded6..0x0056dedc

                HelperRef? sub = null;                                       // 0x0056def4
                bool flag = false;                                           // r4
                bool stop;
                int stopStatus = 0;
                if (originChanged)
                {
                    flag = _stack.Count != 1;                                // 0x0056df04..0x0056df06
                }
                if (originChanged && flag) { stop = true; stopStatus = 0; }  // 0x0056df0c: status 0
                else if (carried != 1) { stop = true; stopStatus = carried; }// 0x0056df12/0x0056dfc0
                else
                {
                    int r = (int)top.UpdateWhileActive(robot, out sub);      // 0x0056df1c
                    if (r != 1) { stop = true; stopStatus = r; }             // 0x0056df22
                    else stop = false;
                }

                if (!stop)
                {
                    if (sub is null) { carried = 1; originChanged = flag; break; }   // 0x0056df28 -> 0x0056e0e6: r8 = end, so the loop ends
                    Info("HelperComponent.UpdateActive.SubHelper", $"Helper {top.Name} delegated to helper {sub.Pointer.Name}");   // 0x0056df6a
                    PushHelperOntoStackAndUpdate(robot, sub);                // 0x0056dfac
                    sub.Release();
                    carried = 1;                                             // 0x0056e0ea
                    originChanged = flag;                                    // 0x0056e0ee
                    continue;
                }

                // the stop step
                Info("HelperComponent.UpdateActive.Complete", $"{top.Name} no longer running");   // 0x0056dfec
                var popped = _stack[^1];
                OutsideLock(() => top.Stop(true));                           // 0x0056e018
                _stack.Remove(popped);                                       // 0x0056e028..0x0056e066
                popped.Release();
                if (_stack.Count != 0)
                    Info("HelperComponent.UpdateActive.ReturningToPrevious", $"returning to helper: {_stack[^1].Pointer.Name}");   // 0x0056e0a6
                originChanged = flag;                                        // 0x0056e0cc: sb = the parked r4
                carried = stopStatus;                                        // 0x0056e0ce
                sub?.Release();
            }

            if (carried == 1) return;                                        // 0x0056e0fa/0x0056e0fe
            if (_stack.Count != 0) return;                                   // 0x0056e100/0x0056e102

            Action<CozmoRobot>? local = null;                                // 0x0056e104/0x0056e10a
            if (carried == 0) { if (_failure is not null) local = _failure; }    // 0x0056e124..0x0056e12e
            else if (carried == 2) { if (_success is not null) local = _success; }   // 0x0056e114..0x0056e11e
            ClearStackMaintenanceVars();                                     // 0x0056e132: the stored callbacks go before the call
            if (local is not null) OutsideLock(() => local(robot));          // 0x0056e138..0x0056e140
        }
    }
}
