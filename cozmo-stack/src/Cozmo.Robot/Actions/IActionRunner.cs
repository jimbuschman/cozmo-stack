namespace Cozmo.Robot;

/// <summary>
/// The engine's full IActionRunner/IAction result values, as the bit patterns the engine stores in +0x18
/// (20261004-actionlist-extraction.md, preamble: "Category means full result shifted24; do not replace full
/// result with category").
/// </summary>
internal static class EngineActionResult
{
    public const uint Success = 0x00000000;
    public const uint Running = 0x01000000;
    public const uint Cancelled = 0x02000000;
    public const uint NotStarted = 0x02000001;
    public const uint BadTag = 0x03000006;
    public const uint Interrupted = 0x03000009;
    public const uint Timeout = 0x03000018;
    public const uint TracksLocked = 0x03000019;
}

/// <summary>
/// The shipped <c>QueueActionPosition</c> (unity/scripts/csharp/Anki.Cozmo/QueueActionPosition.cs:3-11; TBB at
/// 0x0053DA2E bytes <c>03 50 63 5B 6B 73</c> -> targets 0x0053DA34/0x0053DACE/0x0053DAF4/0x0053DAE4/0x0053DB04/
/// 0x0053DB14, B-ACTIONS Q3). Value 2 is NOW_AND_RESUME.
/// </summary>
public enum QueueActionPosition : byte
{
    Now = 0,
    NowAndClearRemaining = 1,
    NowAndResume = 2,
    Next = 3,
    AtEnd = 4,
    InParallel = 5,
}

/// <summary>
/// The queue-facing surface of the engine's <c>IActionRunner</c> (20261004-actionlist-extraction.md rows A/T/Q/C).
/// Only what the queue and the tick need is on it; the rest of the IActionRunner/IAction lifecycle (Init, the
/// 0x03000018 timeout, CheckIfDone, track lock take/release, stop-before-unlock, the concrete destructor) is
/// batch 2 and is deliberately not on this interface.
///
/// Native offsets are identification, not proposed C# layout: state +0x18, RetriesRemain +8, Type +0x44,
/// RequiredTrackMask +0x54, SuppressTrackLocking +0x56, OriginalTag +0x5C, Tag +0x60.
/// </summary>
public interface IActionRunner
{
    /// <summary>+0x18: the stored result; NOT_STARTED 0x02000001, RUNNING 0x01000000, CANCELLED 0x02000000, else a full terminal result.</summary>
    uint State { get; set; }
    /// <summary>+8: the remaining retries, assigned from the queue's byte argument (Q6/Q10/Q11/Q14).</summary>
    byte RetriesRemain { get; set; }
    /// <summary>+0x5C: the tag from the ctor (C1); unchanged by SetTag (C2).</summary>
    uint OriginalTag { get; set; }
    /// <summary>+0x60: the current tag, initially equal to <see cref="OriginalTag"/> (C1); SetTag may replace it (C2).</summary>
    uint Tag { get; set; }
    /// <summary>+0x44: the RobotActionType the queue matches on Cancel(type) (C4).</summary>
    int Type { get; set; }
    /// <summary>+0x54: the required track mask (L1).</summary>
    uint RequiredTrackMask { get; set; }
    /// <summary>+0x56: non-zero skips the track lock test/take and the end release (L5/L16).</summary>
    bool SuppressTrackLocking { get; set; }

    /// <summary>
    /// IActionRunner::Update, run once per queue tick (T6). Returns the FULL result (T8); the implementation also
    /// stores it in <see cref="State"/>. The batch-1 queue only reads the return.
    /// </summary>
    uint Update();
    /// <summary>IActionRunner::Cancel (C3): NOT_STARTED unchanged, otherwise state CANCELLED; no Stop/unlock/callback.</summary>
    void Cancel();
    /// <summary>IAction::Reset(bool) (L2): clears Init/start; true unlocks before NOT_STARTED, false skips the unlock.</summary>
    void Reset(bool unlockTracks);
    /// <summary>The virtual +0x14 interrupt predicate: Q14 interrupts only when it returns true (Q15 falls back to QueueNow).</summary>
    bool CanInterrupt();
    /// <summary>D2: Prep, run before the game snapshot/destructor. Batch 4 (D/W).</summary>
    void Prep();
    /// <summary>
    /// L13/W3/D3: the virtual completion-union getter (the +0x1C cache after Prep). The base implementation in
    /// <see cref="ActionRunner"/> returns the cache; a runner that is not an <see cref="ActionRunner"/> answers 0.
    /// </summary>
    uint GetCompletionUnion() => 0;
    /// <summary>W10/D5: enqueue the destruction event. Batch 4 (D/W).</summary>
    void WatcherEnding();
    /// <summary>L14: release this action's track lock. Batch 2 (L).</summary>
    void UnlockTracks();
    /// <summary>C2: SetTag rules.</summary>
    bool SetTag(uint tag);
}

/// <summary>
/// The global <c>IActionRunner::sTagCounter</c> and the in-use id set (C1, M8-001 B1): seeded 0x002DC6C1 at ELF
/// data 0x01051020 / GOT 0x0103EB14; NextIdTag returns the old value then increments; a wrapped 0 is replaced by
/// 0x002DC6C1. It is global, not per robot. The set is <c>sInUseTagSet</c> 0x0105A8F4.
/// </summary>
internal sealed class ActionRunnerTagCounter
{
    /// <summary>C1: ELF data 0x01051020 seeds the counter 0x002DC6C1 (3,000,001).</summary>
    public const uint Seed = 0x002DC6C1;

    private uint _next;
    private readonly HashSet<uint> _inUse = new();
    // The engine is single-threaded (C1: one global sTagCounter 0x01051020 and sInUseTagSet 0x0105A8F4), so this
    // lock changes no engine behaviour. It only keeps the process-wide static from being corrupted when xUnit runs
    // test classes in parallel and several tests drive ActionRunner ctors/tags at once.
    private readonly object _gate = new();

    public ActionRunnerTagCounter() : this(Seed) { }

    /// <summary>Test seam: start the counter at another value (for the wrap case).</summary>
    internal ActionRunnerTagCounter(uint seed) => _next = seed;

    /// <summary>C1: return the old counter, then increment; a wrapped 0 becomes the seed.</summary>
    public uint NextIdTag()
    {
        lock (_gate)
        {
            uint id = _next;
            uint next = unchecked(_next + 1);
            if (next == 0) next = Seed;
            _next = next;
            return id;
        }
    }

    /// <summary>C1: ctor collision check / C2 uniqueness check.</summary>
    public bool TryReserve(uint id)
    {
        lock (_gate) return id != 0 && _inUse.Add(id);
    }

    /// <summary>C2: erase a prior changed tag (the original stays reserved).</summary>
    public void Release(uint id)
    {
        lock (_gate) _inUse.Remove(id);
    }

    /// <summary>Whether an id is currently reserved (C2 collision).</summary>
    public bool IsInUse(uint id)
    {
        lock (_gate) return _inUse.Contains(id);
    }

    /// <summary>The one global counter every IActionRunner draws from (C1).</summary>
    internal static readonly ActionRunnerTagCounter Global = new();
}

/// <summary>
/// Base for an engine action runner. Batch 2 builds the full <c>IActionRunner::Update</c> lifecycle
/// (20261004-actionlist-extraction.md rows L1-L18): ActionStartUpdating, the start branch's custom motion
/// profile and track lock, the timer, Init/CheckIfDone, the category-4 retry, the terminal completion callbacks
/// and Prep, the end release, the destruction stop-before-unlock, and ForceComplete/RetriesRemain.
///
/// The concrete action supplies Init/CheckIfDone and the track primitives; the queue drives Update. The
/// ActionWatcher hooks (ActionStartUpdating/ActionEndUpdating/ActionEnding, W rows) run through the runner's
/// <see cref="Watcher"/>; a runner without one (the base, and every test fake) is a no-op.
/// </summary>
public abstract class ActionRunner : IActionRunner
{
    /// <summary>An optional log sink, wired by the owner (the engine log).</summary>
    public Action<string>? Log { get; set; }

    private bool _prepped;                       // L13: +0x55 guard
    private uint _completionUnion;               // L13: +0x1C cache
    private readonly List<Action<uint>> _completionCallbacks = new();   // L1: callback list +0x64

    protected ActionRunner(int type, uint requiredTrackMask)
    {
        Type = type;
        RequiredTrackMask = requiredTrackMask;
        // C1: the ctor takes a new id from the global counter and collision-checks the in-use set; original +0x5C
        // and current +0x60 start equal.
        uint tag;
        do { tag = ActionRunnerTagCounter.Global.NextIdTag(); }
        while (!ActionRunnerTagCounter.Global.TryReserve(tag));
        OriginalTag = tag;
        Tag = tag;
    }

    public uint State { get; set; } = EngineActionResult.NotStarted;
    public byte RetriesRemain { get; set; }
    public uint OriginalTag { get; set; }
    public uint Tag { get; set; }
    public int Type { get; set; }
    public uint RequiredTrackMask { get; set; }
    public bool SuppressTrackLocking { get; set; }

    // L2/L7: IAction +0x74, the start time; negative from the ctor/reset, stamped from the engine clock at the
    // first UpdateInternal before Init. L7/L9: +0x70 initialized.
    public float StartTime { get; set; } = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
    public bool Initialized { get; set; }

    // L8: virtual pre-delay +0x24, post-delay +0x28 (read only when initialized) and timeout +0x2C. The base
    // defaults are 0, 0 and 30.0 s (0x41F00000, 0x0052B0BA..0x0052B0C8).
    protected virtual float PreDelaySeconds => 0f;
    protected virtual float PostDelaySeconds => 0f;
    public float TimeoutSeconds { get; set; } = BitConverter.Int32BitsToSingle(unchecked((int)0x41F00000));

    /// <summary>The engine clock (<c>BaseStationTimer::GetCurrentTimeInSeconds</c>, 0x00540D4A..0x00540D4E).</summary>
    protected virtual float EngineClockSeconds => 0f;

    // fidelity: M7-020
    // W7/W8/W9: the watcher this runner reports its nesting and destruction to. The stack's seam for the engine's
    // robot+0x250+0x10 path: ActionList::QueueAction sets it on the incoming runner (IActionRunner::
    // GetRobotCompletedActionMessage 0x540AC2 [r0,#4] -> robot+0x250 ActionList -> [r0,#0x10] watcher), and a
    // compound propagates it to its children (W7). A runner without one (the base, and every test fake) is a no-op.
    // W13: registration is not gated by the external interface.
    internal virtual ActionWatcher? Watcher { get; set; }

    // L5/L14/L15: the MovementComponent primitives, overridden by a concrete action that owns a robot.
    protected virtual bool AreAnyTracksLocked(uint mask) => false;
    protected virtual void LockTracks(uint mask, string owner) { }
    protected virtual void UnlockTracksInternal(uint mask, string owner) { }
    protected virtual bool AreAllTracksLockedBy(uint mask, string owner) => false;
    protected virtual void StopHeadTrack() { }
    protected virtual void StopLiftTrack() { }
    protected virtual void StopBodyTrack() { }
    protected virtual bool HeadTrackMoving => false;
    protected virtual bool LiftTrackMoving => false;
    protected virtual bool BodyTrackMoving => false;
    protected virtual bool HasCustomMotionProfile => false;

    // L6: the lock/unlock owner is to_string(int) of the signed tag; the destructor stop-owner is to_string(unsigned).
    private string LockOwner => ((int)Tag).ToString();
    private string StopOwner => ((uint)Tag).ToString();

    // L9: the concrete Init (base Init at 0x005413CA returns 0) and CheckIfDone (pure virtual). L13: the base
    // completion-union getter copies +0x1C. L4: SetMotionProfile returns false when there is no custom profile.
    public virtual int Init() => 0;
    public abstract uint CheckIfDone();
    public virtual uint GetCompletionUnion() => _completionUnion;
    public virtual bool SetMotionProfile() => false;

    /// <summary>L12: AddCompletionCallback appends; RunCallbacks loops the list in order with the full result.</summary>
    public void AddCompletionCallback(Action<uint> callback) => _completionCallbacks.Add(callback);
    public virtual void RunCompletionCallbacks(uint result)
    {
        foreach (var cb in _completionCallbacks) cb(result);
    }

    // L18: ForceComplete writes SUCCESS.
    public void ForceComplete() => State = EngineActionResult.Success;
    // L18: RetriesRemain decrements a positive byte and returns true, else false.
    public bool TakeRetry()
    {
        if (RetriesRemain == 0) return false;
        RetriesRemain--;
        return true;
    }

    /// <summary>
    /// IActionRunner::Update 0x00540370..0x0054063A (L3-L13). ActionStartUpdating first; RUNNING goes straight
    /// to UpdateInternal; only NOT_STARTED, INTERRUPTED 0x03000009 and 0x04000000 enter the start branch; any
    /// other stored state Preps and ActionEndUpdatings without CheckIfDone.
    /// </summary>
    public virtual uint Update()
    {
        Watcher?.ActionStartUpdating(this);

        uint result;
        if (State == EngineActionResult.Running)
        {
            result = UpdateInternal();
        }
        else if (State == EngineActionResult.NotStarted || State == EngineActionResult.Interrupted || State == 0x04000000u)
        {
            // L4: custom motion profile; a false return logs unused, not failure.
            if (HasCustomMotionProfile && !SetMotionProfile())
                Log?.Invoke("info: IActionRunner.Update.MotionProfileUnused");
            // L4: store RUNNING BEFORE the lock check.
            State = EngineActionResult.Running;
            // L5: +0x56 bypasses AreAnyTracksLocked and LockTracks; otherwise a locked required mask fails
            // 0x03000019 and ends the updating without Init (Delete Preps later).
            if (!SuppressTrackLocking)
            {
                if (AreAnyTracksLocked(RequiredTrackMask))
                {
                    Log?.Invoke("warning: IActionRunner.Update.TracksLocked");
                    State = EngineActionResult.TracksLocked;
                    Watcher?.ActionEndUpdating();
                    return State;
                }
                LockTracks(RequiredTrackMask, LockOwner);
            }
            result = UpdateInternal();
        }
        else
        {
            // L3: a terminal stored state -> Prep/ActionEndUpdating without CheckIfDone.
            Prep();
            Watcher?.ActionEndUpdating();
            return State;
        }

        // L13: store the virtual result over +0x18. The native always calls ActionEndUpdating after storing (a
        // RUNNING result branches at 0x005405A2 to 0x54062C); only PrepForCompletion (0x540626) is skipped for
        // RUNNING.
        State = result;
        if (result != EngineActionResult.Running) Prep();
        Watcher?.ActionEndUpdating();
        return result;
    }

    /// <summary>
    /// IAction::UpdateInternal 0x00540D1C..0x00540F9E (L7-L13): stamp a negative start before Init; read the
    /// delays; the timeout first (at equality), then the pre+post wait; then Init once and CheckIfDone the same
    /// tick; the category-4 retry is transient NOT_STARTED and returns RUNNING; a terminal result runs the
    /// callbacks with the full result.
    ///
    /// Virtual: the compounds (20261004-actionlist-extraction.md S3/S4, R1/R2) override it with their own child
    /// tick instead of the timer/Init/CheckIfDone.
    /// </summary>
    protected virtual uint UpdateInternal()
    {
        float now = EngineClockSeconds;
        if (StartTime < 0f) StartTime = now;                       // L7: stamp BEFORE Init
        float pre = PreDelaySeconds;                               // L8: +0x24
        float post = Initialized ? PostDelaySeconds : 0f;          // L8: +0x28 only when initialized
        float timeout = TimeoutSeconds;                            // L8: +0x2C

        // L8/L10: FIRST now >= f32(start + timeout) -> timeout, including equality.
        if (now >= StartTime + timeout) return Finish(EngineActionResult.Timeout);
        // L8: else now < f32(f32(start + pre) + post) -> RUNNING wait.
        if (now < (StartTime + pre) + post) return EngineActionResult.Running;

        // L9: gates pass.
        if (!Initialized)
        {
            int r = Init();
            if (r == 0) Initialized = true;
            if (Initialized) return Finish(CheckIfDone());
            return Finish(unchecked((uint)r));                     // a nonzero Init with init still false
        }
        return Finish(CheckIfDone());                              // already init
    }

    /// <summary>L11 retry and L12 terminal callbacks; the outer Update stores the returned result over +0x18.</summary>
    private uint Finish(uint result)
    {
        if ((result >> 24) == 4 && RetriesRemain > 0)
        {
            RetriesRemain--;
            StartTime = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
            Initialized = false;
            State = EngineActionResult.NotStarted;                 // transient; outer stores RUNNING
            return EngineActionResult.Running;
        }
        RunCompletionCallbacks(result);
        return result;
    }

    /// <summary>
    /// L13 PrepForCompletion 0x00540750..0x005407C2: the +0x55 guard; the first call copies the virtual
    /// GetCompletionUnion into the +0x1C cache, a repeat logs AlreadyPrepped. NO RunCallbacks here.
    /// </summary>
    public virtual void Prep()
    {
        if (_prepped)
        {
            Log?.Invoke("debug: IActionRunner.PrepForCompletion.AlreadyPrepped");
            return;
        }
        _completionUnion = GetCompletionUnion();
        _prepped = true;
    }

    /// <summary>The virtual +0x14 interrupt predicate; the concrete override says whether Interrupt may take it.</summary>
    public abstract bool CanInterrupt();

    // C3: runner Cancel 0x005409DC..0x00540A46: NOT_STARTED unchanged; otherwise log then state CANCELLED; no
    // Stop/unlock/CheckIfDone/RunCallbacks directly.
    public virtual void Cancel()
    {
        if (State == EngineActionResult.NotStarted) return;
        Log?.Invoke("info: IActionRunner.Cancel: action cancelled");
        State = EngineActionResult.Cancelled;
    }

    // C2: SetTag 0x00540098..0x00540192. All three refusal paths set the state to BAD_TAG 0x03000006 and return
    // false: RUNNING (0x005400B2 -> 0x0054018A), requested zero (0x0054011E -> 0x0054018A) and collision
    // (0x00540136 -> 0x00540140 -> 0x0054018A). Otherwise erase the prior changed tag (the original remains
    // reserved) and a non-zero unique requested tag stores +0x60/true.
    public virtual bool SetTag(uint requested)
    {
        if (State == EngineActionResult.Running)
        {
            Log?.Invoke("warning: IActionRunner.SetTag.Running");
            State = EngineActionResult.BadTag;
            return false;
        }
        if (Tag != OriginalTag) ActionRunnerTagCounter.Global.Release(Tag);
        if (requested == 0 || ActionRunnerTagCounter.Global.IsInUse(requested))
        {
            Log?.Invoke("warning: IActionRunner.SetTag.BadTag");
            State = EngineActionResult.BadTag;
            return false;
        }
        Tag = requested;
        ActionRunnerTagCounter.Global.TryReserve(requested);
        return true;
    }

    /// <summary>L2 Reset 0x00540CE8: clear Init/start; true unlocks before NOT_STARTED, false skips the unlock.</summary>
    public virtual void Reset(bool unlockTracks)
    {
        StartTime = BitConverter.Int32BitsToSingle(unchecked((int)0xBF800000));
        Initialized = false;
        if (unlockTracks) UnlockTracks();
        State = EngineActionResult.NotStarted;
    }

    /// <summary>L14 UnlockTracks 0x005408EC: skip when +0x56 or NOT_STARTED, otherwise release mask/tag.</summary>
    public virtual void UnlockTracks()
    {
        if (SuppressTrackLocking) return;
        if (State == EngineActionResult.NotStarted) return;
        UnlockTracksInternal(RequiredTrackMask, LockOwner);
    }

    /// <summary>
    /// P7: a compound's retained child (DeleteOnCompletion false, non-NOT_STARTED, non-suppressed) tests the
    /// tracks it holds with the UNSIGNED stop-owner string (the same owner the destructor's stop gate uses,
    /// L6/L15) and re-takes them if it no longer holds them, so the child's own destructor can stop the moving
    /// track. The owner for the re-take is the same unsigned string: it is what the stop gate tests.
    /// </summary>
    internal bool OwnsTracksByUnsignedOwner() => AreAllTracksLockedBy(RequiredTrackMask, StopOwner);
    internal void RetakeTracks() => LockTracks(RequiredTrackMask, StopOwner);

    /// <summary>
    /// The ~IActionRunner tail 0x00541084..0x0054127A (L15/L16). The queue's DeleteActionAndIter calls this as
    /// the virtual deleting destructor: stop each moving track this action owns (HEAD then LIFT then BODY, using
    /// the unsigned stop-owner), then release the declared mask unless +0x56 or NOT_STARTED, then the watcher's
    /// ActionEnding (W10/D5).
    /// </summary>
    public virtual void WatcherEnding()
    {
        // L15: the stop checks are NOT gated by +0x56.
        if (HeadTrackMoving && AreAllTracksLockedBy(1, StopOwner)) StopHeadTrack();
        if (LiftTrackMoving && AreAllTracksLockedBy(2, StopOwner)) StopLiftTrack();
        if (BodyTrackMoving && AreAllTracksLockedBy(4, StopOwner)) StopBodyTrack();
        // L16: the declared-mask release after the stops.
        if (!SuppressTrackLocking && State != EngineActionResult.NotStarted)
            UnlockTracksInternal(RequiredTrackMask, LockOwner);
        // W10/D5: the watcher's ActionEnding enqueue, after the stop/unlock tail and independent of the game gate.
        Watcher?.ActionEnding(this);
    }
}
