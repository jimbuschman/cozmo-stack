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

    public ActionRunnerTagCounter() : this(Seed) { }

    /// <summary>Test seam: start the counter at another value (for the wrap case).</summary>
    internal ActionRunnerTagCounter(uint seed) => _next = seed;

    /// <summary>C1: return the old counter, then increment; a wrapped 0 becomes the seed.</summary>
    public uint NextIdTag()
    {
        uint id = _next;
        uint next = unchecked(_next + 1);
        if (next == 0) next = Seed;
        _next = next;
        return id;
    }

    /// <summary>C1: ctor collision check / C2 uniqueness check.</summary>
    public bool TryReserve(uint id) => id != 0 && _inUse.Add(id);

    /// <summary>C2: erase a prior changed tag (the original stays reserved).</summary>
    public void Release(uint id) => _inUse.Remove(id);

    /// <summary>Whether an id is currently reserved (C2 collision).</summary>
    public bool IsInUse(uint id) => _inUse.Contains(id);

    /// <summary>The one global counter every IActionRunner draws from (C1).</summary>
    internal static readonly ActionRunnerTagCounter Global = new();
}

/// <summary>
/// Base for an engine action runner. It implements only the parts the queue and the tag rules need in batch 1:
/// C1 (the ctor tag), C2 (SetTag) and C3 (Cancel). Init, the timeout, CheckIfDone, the track lock, Reset's body
/// and the destructor are batch 2 and are left abstract or as marked no-ops; the queue drives the fake in tests.
/// </summary>
public abstract class ActionRunner : IActionRunner
{
    /// <summary>An optional log sink, wired by the owner (the engine log).</summary>
    public Action<string>? Log { get; set; }

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

    /// <summary>IActionRunner::Update; the concrete action is batch 2.</summary>
    public abstract uint Update();

    /// <summary>The virtual +0x14 interrupt predicate; the concrete override is batch 2.</summary>
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

    // L2: the body is batch 2 (clear Init/start, optional unlock). Marked, not invented.
    public virtual void Reset(bool unlockTracks) { }
    // D2: Prep is batch 4.
    public virtual void Prep() { }
    // W10/D5: the watcher event is batch 4.
    public virtual void WatcherEnding() { }
    // L14: the track-lock release is batch 2.
    public virtual void UnlockTracks() { }
}
