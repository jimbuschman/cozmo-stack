using System.Collections.Concurrent;
using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// How an action ended, as far as the inventory settles it. <c>IActionRunner::Update</c> 0x00540370 ends an
/// action whose required tracks are locked with <c>0x03000019</c> (TRACKS_LOCKED, 0x00540572..0x0054057c) and
/// <c>IAction::UpdateInternal</c> 0x00540d1c ends a timed-out one with <c>0x03000018</c> (TIMEOUT). A failure the
/// inventory does not give a code for carries none.
/// </summary>
// fidelity: M8-007
public readonly record struct ActionOutcome(bool Success, uint? Result)
{
    /// <summary>ActionResult TRACKS_LOCKED (EnumToString(ActionResult) 0x007586ac).</summary>
    public const uint TracksLocked = 0x03000019;
    /// <summary>ActionResult TIMEOUT.</summary>
    public const uint TimedOut = 0x03000018;
    /// <summary><c>TriggerAnimationAction::Init</c> 0x0054443c returns 0x0300000C for an empty animation group or clip name (0x00544444..0x00544448; warning "TriggerAnimationAction.NoAnimationForTrigger" 0x005445f4).</summary>
    public const uint NoAnimationForTrigger = 0x0300000C;
    /// <summary>0x03000001: <c>SetStreamingAnimation</c> refused the stream (<c>PlayAnimationAction::Init</c> 0x00543ffc, 0x00544132).</summary>
    public const uint StreamRefused = 0x03000001;

    public static readonly ActionOutcome Succeeded = new(true, 0);
    public static ActionOutcome Failed(uint? result = null) => new(false, result);
}

/// <summary>
/// A behaviour written the way the engine's <c>BehaviorReactToX</c> classes are written: a chain of
/// transitions, each starting one action (<c>TriggerAnimationAction</c>, <c>WaitAction</c>,
/// <c>CalibrateMotorAction</c>) whose completion calls the next transition, and a per-tick
/// <c>UpdateInternal</c> for the classes that poll the robot in between.
///
/// The engine's <c>IBehavior::StartActing</c> runs an action and calls a member function when it ends;
/// <c>StopActing</c> cancels it; a behaviour with no action running completes. Those three things are
/// reproduced here, so each shipped class can be transcribed transition by transition rather than
/// re-designed. Completion callbacks are queued and run inside <see cref="Update"/>, on the manager's
/// thread and clock, which is where the engine's run too.
/// </summary>
public abstract class SteppedBehavior : IBehavior
{
    private readonly object _gate = new();
    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly List<string> _trace = new();

    private CozmoAnimations? _animations;
    private long _generation;
    private bool _owns;
    private volatile bool _acting;

    private Action? _onWaitDone;
    private double _waitRemainingMs = double.NaN, _waitDeadlineMs = double.NaN;

    private Func<bool>? _waitCondition;
    private Action<bool>? _onConditionDone;
    private double _conditionTimeoutMs, _conditionDeadlineMs = double.NaN;

    // TriggerAnimationAction's timeout_s (IAction+0x74, written by the constructor). The engine passes
    // 60.0 for the reaction animations; null means the engine's "no timeout".
    private double _actionTimeoutMs = double.NaN;
    private double _actionDeadlineMs = double.NaN;
    private Action<ActionOutcome>? _onActionTimeout;

    private volatile bool _finished = true;

    // The engine's StopActing(false, false) cancels the action without its callback. Each started action
    // remembers the epoch it began in; a stop advances the epoch and the stale completion is dropped.
    private int _actionEpoch;

    // The IBehavior engine fields the M8 rows name, kept beside the stack's own run state so the
    // lifecycle transitions are reproducible and testable. IBehavior +0xa1 running, +0xa0 acting-without-
    // action, +0x34 running-penalty clock, +0x114 resume counter, +0x118 resume suppression, +0x80
    // successful-Init counter, +0xd8 spark gate, +0x104 running-score bonus.
    private bool _engineRunning;
    private bool _engineActingFlag;
    private double _runningPenaltyClockSec;
    private int _resumeCount;
    // IBehavior +0x118 is a float (vstr s0,[r4,#0x118] 0x005bcf4c; vcmpe at 0x005bd8be).
    private float _resumeSuppressionUntil;
    private int _scoreIncreaseCount;
    private bool _sparkDisabled;
    private bool _resuming;
    // IBehavior +0x84: the current-action handle (0 = none). Each StartActing takes a new non-zero value, so
    // StopActing can tell whether a handle is still the one it cancelled (0x005bd3f4..0x005bd3fe).
    private int _currentActionHandle;
    private int _actionHandleCounter;
    // IBehavior +0x98: the std::function StartActing stores (operator= into +0x88, 0x005bdb40..0x005bdb46) and
    // HandleActionComplete invokes (0x005be216..0x005be21c). Init (0x005bccc6..0x005bcce6) and StopOnNextActionComplete
    // (0x005bd694..0x005bd6b4) destroy it. Here the callbacks live in the actions' closures; each captures this epoch when its
    // action starts, and destroying +0x98 advances the epoch, so a completion that finds a newer epoch does not call back.
    private int _callbackEpoch;
    private object? _sharedHandle;

    /// <summary>
    /// A seam the inventory leaves open is reported here, once per message per process, instead of being answered silently. Tests and
    /// tools can subscribe.
    /// </summary>
    public static event Action<string>? MissingReported;
    private static readonly HashSet<string> MissingSeen = new();
    internal static void ResetMissingForTests() { lock (MissingSeen) MissingSeen.Clear(); }
    private static string ReportMissingAndReturn(string value, string what) { ReportMissing(what); return value; }
    internal static void ReportMissing(string what)
    {
        lock (MissingSeen) if (!MissingSeen.Add(what)) return;
        MissingReported?.Invoke("MISSING: " + what);
    }

    protected SteppedBehavior(string id, string behaviorClass)
    {
        Id = id;
        Class = behaviorClass;
    }

    public string Id { get; }
    public string Class { get; }

    /// <summary>
    /// The flat score (+0x100). The engine's default is zero: <c>IBehavior::IBehavior</c> 0x005BBD28 and
    /// <c>EvaluateScoreInternal</c> 0x005BEEC2 (M8-004). Only a config's <c>flatScore</c> (0x005BC4E0) or a caller sets it.
    /// </summary>
    // fidelity: M8-004
    public double Score { get; set; }

    /// <summary>
    /// The clock used for timings that outlive one run (a behaviour that remembers when it last ran).
    /// Milliseconds. Settable so a test can wind it.
    /// </summary>
    public Func<double> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>What the behaviour has done this run, one line per step. For tests and the conformance tools.</summary>
    public IReadOnlyList<string> Trace { get { lock (_gate) return _trace.ToArray(); } }

    /// <summary>Raised for every trace line as it is written.</summary>
    public event Action<string>? Step;

    /// <summary>The context and scope of the current run. Valid from <see cref="OnStart"/> until stopped.</summary>
    protected BehaviorContext Context { get; private set; } = null!;

    /// <summary>
    /// <c>IBehavior::NeedActionCompleted</c> 0x005BE40C: report a needs action to the needs manager - the one
    /// named here, or this behaviour's own <c>needsActionID</c> from its shipped config when none is named.
    /// Returns what was reported, or null when there was nothing to report.
    /// </summary>
    protected string? NeedActionCompleted(string? actionId = null, string? fallback = null) =>
        BehaviorNeedsActions.Complete(this, Context, actionId, fallback);
    protected BehaviorScope Scope { get; private set; } = null!;

    /// <summary>The manager's clock as of the current <see cref="Update"/>, milliseconds.</summary>
    protected double NowMs { get; private set; }

    /// <summary>The manager's clock at the first <see cref="Update"/> of this run, or null before it.</summary>
    protected double? StartedMs { get; private set; }

    /// <summary>Whether an animation started by this behaviour is still playing.</summary>
    protected bool Acting => _acting;

    /// <summary>Whether anything is in flight: an animation, a wait, a condition, or a queued callback.</summary>
    protected bool Busy => _acting || _onWaitDone is not null || _waitCondition is not null || !_pending.IsEmpty;

    /// <summary>
    /// The engine completes a behaviour that is not acting. A class whose <c>UpdateInternal</c> polls the
    /// robot between actions overrides this to stay alive; it then ends itself with <see cref="Finish"/>.
    /// </summary>
    protected virtual bool KeepsRunningWithoutAction => false;

    public virtual bool IsRunnable(BehaviorContext context) =>
        context.Robot.Animations.Library is not null && IsRunnableBase(context) && IsRunnableInternal(context);

    /// <summary>The engine's <c>IsRunnableInternal</c> (the <c>vtable+0x50</c> gate). Most reaction classes just return true.</summary>
    protected virtual bool IsRunnableInternal(BehaviorContext context) => true;

    // ============================================================== the IBehavior engine lifecycle (M8-001)

    /// <summary>
    /// <c>IBehavior::IsRunnableBase</c> 0x005bd778: the running flag <c>+0xa1</c> short-circuits to true
    /// (<c>ldrb.w r0,[r4,#0xa1]</c> 0x005bd780); otherwise the required AI process (<c>+0x1c</c> against
    /// <c>robot+0x264+8</c>, <c>AIInformationAnalyzer::IsProcessRunning</c> 0x005bd7de), the robot state
    /// byte <c>+0x74</c> against 3 or <c>robot+0x264+0x30+0x14</c> (0x005bd7e6..0x005bd7f6), the unlock id
    /// <c>+0x70</c> (0x55 bypasses) through <c>ProgressionUnlockComponent::IsUnlocked(robot+0x448, id,
    /// true)</c> (0x005bd810), the float timers <c>+0x78</c>/<c>+0x7c</c> (0x005bd81a..), the wants-to-run
    /// strategy <c>+0x38</c> (0x005bd8a4), and finally <c>now &gt;= +0x118</c> (0x005bd8b4..0x005bd8c8).
    /// The robot/AI/progression inputs are cross-layer seams; a null seam means "no such requirement".
    /// </summary>
    // fidelity: M8-001
    public bool IsRunnableBase(BehaviorContext context)
    {
        if (_engineRunning) return true;                                          // +0xa1
        if (RequiredProcessRunning is { } process && !process()) return false;    // +0x1c
        if (RobotStateAllowsRun is { } state && !state()) return false;           // +0x74
        if (UnlockAllowsRun is { } unlock && !unlock()) return false;             // +0x70
        if (!RecentTimersAllow(context)) return false;                            // +0x78/+0x7c
        // The three robot-byte gates and their behaviour virtuals (decomp 0x005bd864..0x005bd89c):
        // robot+0x355 -> vtable+0x20, robot+0x34a -> vtable+0x24, robot+0x284->+8 != -1 -> vtable+0x28.
        // robot+0x355 is the off-treads state and [robot+0x284]+8 != -1 is "carrying" (CarryingComponent, wired through
        // Motion.IsCarryingObject) and robot+0x34a is Sensors.OnChargerPlatform; the seams override them. The AI process, the +0x74 state, the +0x70 unlock, the
        // +0x78/+0x7c timers and the spark gate have no source in this stack: with no seam they are reported MISSING, not allowed.
        bool notOnTreads = RobotState355?.Invoke() ?? context.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads;
        if (notOnTreads && !RunnableGate20(context)) return false;
        // robot+0x34a is OnChargerPlatform (SetOnChargerPlatform 0x00511d4c..0x00511d6a), which Sensors.OnChargerPlatform carries.
        bool onChargerPlatform = RobotState34a?.Invoke() ?? context.Robot.Sensors.OnChargerPlatform;
        if (onChargerPlatform && !RunnableGate24(context)) return false;
        bool carrying = RobotComponent284?.Invoke() ?? context.Robot.Motion.IsCarryingObject?.Invoke() == true;
        if (carrying && !RunnableGate28(context)) return false;
        if (RequiredProcessRunning is null || RobotStateAllowsRun is null || UnlockAllowsRun is null)
            ReportMissing("IBehavior::IsRunnableBase AI-process (0x005bd7de), +0x74 state (0x005bd7e6) and +0x70 unlock (0x005bd810) inputs: no source in this stack; not tested");
        if (!WantsToRun(context)) return false;                                   // +0x38
        double now = context.ClockSec?.Invoke() ?? Clock() / 1000.0;
        return (float)now >= _resumeSuppressionUntil;                             // +0x118
    }

    /// <summary>The float timers' check (<c>+0x78</c>/<c>+0x7c</c>). A class that reads them from its config overrides it.</summary>
    protected virtual bool RecentTimersAllow(BehaviorContext context) => RecentTimersAllowRun?.Invoke() ?? true;

    /// <summary>The wants-to-run strategy's check (<c>+0x38</c>). A class that builds a strategy from its config overrides it.</summary>
    protected virtual bool WantsToRun(BehaviorContext context) => WantsToRunAllowsRun?.Invoke() ?? true;

    /// <summary>The AI process seam (<c>+0x1c</c> against <c>robot+0x264+8</c>); null = no required process.</summary>
    public Func<bool>? RequiredProcessRunning { get; set; }
    /// <summary>The robot-state seam (<c>+0x74</c> against 3 or the AI state); null = allow.</summary>
    public Func<bool>? RobotStateAllowsRun { get; set; }
    /// <summary>The progression-unlock seam (<c>+0x70</c> through <c>IsUnlocked</c>); null = allow.</summary>
    public Func<bool>? UnlockAllowsRun { get; set; }
    /// <summary>The recent-event timer seam (<c>+0x78</c>/<c>+0x7c</c>); null = allow.</summary>
    public Func<bool>? RecentTimersAllowRun { get; set; }
    /// <summary>The wants-to-run strategy seam (<c>+0x38</c>); null = allow.</summary>
    public Func<bool>? WantsToRunAllowsRun { get; set; }
    /// <summary>Robot byte <c>+0x355</c> (0x005bd864); when true the <c>vtable+0x20</c> gate must pass. Null = not modelled.</summary>
    public Func<bool>? RobotState355 { get; set; }
    /// <summary>Robot byte <c>+0x34a</c> (0x005bd876); when true the <c>vtable+0x24</c> gate must pass. Null = <c>Sensors.OnChargerPlatform</c>.</summary>
    public Func<bool>? RobotState34a { get; set; }
    /// <summary>Robot component <c>+0x284</c> whose <c>+8</c> is checked (0x005bd888); when true the <c>vtable+0x28</c> gate must pass. Null = not modelled.</summary>
    public Func<bool>? RobotComponent284 { get; set; }
    /// <summary>
    /// The <c>vtable+0x20</c> runnable gate; the engine calls it when robot+0x355 is set. The engine's base slot is 0x005bf04c (<c>movs r0,#0</c>); each class's own vtable
    /// (relocation at vtable start + 8 + 0x20) gives its value, tabulated in <see cref="EngineRunnableGates"/>. A class that is not in the table keeps the base's 0 and
    /// reports MISSING once.
    /// </summary>
    // fidelity: M8-001
    protected virtual bool RunnableGate20(BehaviorContext context)
    {
        if (EngineRunnableGates.Table.TryGetValue(Class, out var g)) return g.Slot20;
        ReportMissing($"{GetType().Name} (class '{Class}'): vtable+0x20 gate - the class is not in the table of the 79 engine vtables; the engine's base slot 0x005bf04c returns 0 and that is what is answered");
        return false;
    }
    /// <summary>The <c>vtable+0x24</c> runnable gate; the engine calls it when robot+0x34a is set. The base slot is 0x0059ec12 (<c>movs r0,#0</c>); the class's own value is in <see cref="EngineRunnableGates"/>.</summary>
    // fidelity: M8-001
    protected virtual bool RunnableGate24(BehaviorContext context)
    {
        if (EngineRunnableGates.Table.TryGetValue(Class, out var g)) return g.Slot24;
        ReportMissing($"{GetType().Name} (class '{Class}'): vtable+0x24 gate - the class is not in the table of the 79 engine vtables; the engine's base slot 0x0059ec12 returns 0 and that is what is answered");
        return false;
    }
    /// <summary>
    /// The <c>vtable+0x28</c> runnable gate; the engine calls it when robot+0x284->+8 is not -1. The engine's base slot is <c>__cxa_pure_virtual</c> (every concrete class overrides it);
    /// a class that is not in <see cref="EngineRunnableGates"/> has no value to give, reports MISSING once and answers false (the engine's call would terminate). FindFaces and
    /// ExploreLookAroundInPlace read a config byte (+0x120, <c>behavior_CanCarryCube</c>, 0x005e1efe) and override this.
    /// </summary>
    // fidelity: M8-001
    protected virtual bool RunnableGate28(BehaviorContext context)
    {
        if (EngineRunnableGates.Table.TryGetValue(Class, out var g))
        {
            if (g.Slot28 is { } v) return v;
            ReportMissing($"{GetType().Name} (class '{Class}'): vtable+0x28 gate is a function (0x005c1d76, ldrb r0,[r0,#0x120]) that this class does not override");
            return false;
        }
        ReportMissing($"{GetType().Name} (class '{Class}'): vtable+0x28 gate - the class is not in the table of the 79 engine vtables and the engine's base slot is __cxa_pure_virtual; answered false");
        return false;
    }

    /// <summary>IBehavior +0xa1: the engine's running flag, as Init/Stop/Resume set it.</summary>
    public bool EngineRunning => _engineRunning;
    /// <summary>
    /// The <c>vtable+0x48</c> (<c>InitInternal</c>) failure signal. <c>IBehavior::Init</c> 0x005bcb54 calls
    /// <c>InitInternal</c> (<c>ldr r2,[r2,#0x48]; blx r2</c> 0x005bccfe/0x005bcd00); a non-zero result clears
    /// <c>+0xa1</c> and is <c>Init</c>'s return (0x005bcd04/0x005bcd06, 0x005bcd5c), which the manager turns
    /// into <c>BehaviorManager.SetCurrentBehavior.InitFailed</c> (0x005a1e98). <see cref="OnStart"/> is this
    /// stack's <c>InitInternal</c> and returns nothing, so a class whose <c>InitInternal</c> can return
    /// non-zero sets this to true inside it. <see cref="InitLifecycle"/> clears it before the call.
    /// </summary>
    // fidelity: M8-001
    public bool InitFailed { get; set; }
    /// <summary>IBehavior +0xd8: the spark gate Init computed (from the AI process).</summary>
    public bool SparkDisabled => _sparkDisabled;
    /// <summary>IBehavior +0x80: incremented by Init each time <c>InitInternal</c> returns zero.</summary>
    public int ScoreIncreaseCount => _scoreIncreaseCount;
    /// <summary>IBehavior +0x34: the running-penalty clock, stamped by Init and the Resume normal path.</summary>
    public double RunningPenaltyClockSec => _runningPenaltyClockSec;
    /// <summary>IBehavior +0x114: how many CliffDetected/UnexpectedMovement resumes have been counted.</summary>
    public int ResumeCount => _resumeCount;
    /// <summary>IBehavior +0x118: the TooManyResumesCliffOrMovement suppression stamp (a float).</summary>
    public double ResumeSuppressionUntilSec => _resumeSuppressionUntil;
    /// <summary>IBehavior +0x104: the running-score bonus <see cref="IncreaseScoreWhileActing"/> adds to.</summary>
    public double RunningScoreBonus { get { lock (_bonusGate) return _runningScoreBonus; } }
    private readonly object _bonusGate = new();
    private double _runningScoreBonus;
    /// <summary>IBehavior +0xa2: whether <see cref="Resume"/> is inside its normal path.</summary>
    public bool ResumeInProgress => _resuming;
    /// <summary>IBehavior +0x84: whether a current-action handle is set.</summary>
    public bool HasCurrentAction => Volatile.Read(ref _currentActionHandle) != 0;

    /// <summary>
    /// The refusal cooldown, 15.0f (<c>vmov.f32 s0,#15.0</c> 0x005bcf36 = 0x41700000): a refused cliff or
    /// unexpected-movement resume sets <c>+0x118 = now + 15.0f</c> (<c>vadd.f32</c>/<c>vstr</c> 0x005bcf48..0x005bcf4c).
    /// </summary>
    public static readonly float TooManyResumesCooldownSec = BitConverter.Int32BitsToSingle(0x41700000);

    /// <summary>The event the refusal raises: <c>MoodManager::TriggerEmotionEvent("TooManyResumesCliffOrMovement", now)</c> 0x005bcf68.</summary>
    public const string TooManyResumesEventName = "TooManyResumesCliffOrMovement";

    /// <summary>
    /// <c>IBehavior::IncreaseScoreWhileActing</c> 0x005bf02c: adds <paramref name="amount"/> to +0x104 only
    /// while the current-action handle +0x84 is non-zero (<c>ldr.w r2,[r0,#0x84]</c> 0x005bf02c;
    /// <c>cbz r2,#0x5bf042</c> 0x005bf030; <c>vadd.f32</c>/<c>vstr</c> 0x005bf03a/0x005bf03e). Its only
    /// engine callers are M7/M15 concrete behaviours (KnockOverCubes 10.0f, PopAWheelie/PutDownBlock/
    /// RollBlock/StackBlocks 0.8f/5.0f, and the FUN_0059ec54 helper); nothing in M8 calls it.
    /// </summary>
    // fidelity: M8-003
    public void IncreaseScoreWhileActing(double amount)
    {
        lock (_bonusGate) { if (HasCurrentAction) _runningScoreBonus += amount; }   // +0x84 non-zero
    }

    /// <summary>
    /// <c>IBehavior::ScoredActingStateChanged(bool)</c> 0x005bf044, <c>IBehavior</c> vtable slot +0x80
    /// (relocation 0x01026568; no behaviour class overrides it). It ignores its argument and clears +0x104
    /// (<c>movs r1,#0</c>; <c>str.w r1,[r0,#0x104]</c> 0x005bf044..0x005bf04a). It has two callers, both through
    /// the slot: <c>StopActing</c> with false (0x005bd358..0x005bd360, so every stop of an action clears the
    /// bonus), <c>StartActing</c> with true (0x005bdb54..0x005bdb5c, so every action start does too) and
    /// <c>HandleActionComplete</c> 0x005be1e6 with false (0x005be202..0x005be208, after it stores 0 to +0x84: a completed action clears it too).
    /// </summary>
    // fidelity: M8-003, M8-001
    public void ScoredActingStateChanged(bool acting) { lock (_bonusGate) _runningScoreBonus = 0; }

    /// <summary>IBehavior +0x10c: this behaviour's objective (from its config), default 0x29 invalid.</summary>
    public BehaviorObjective BehaviorObjective { get; set; } = BehaviorObjective.Invalid;

    /// <summary>
    /// <c>IBehavior::HandleBehaviorObjective</c> 0x005bf00c: when this behaviour's <c>+0x10c</c> is not
    /// 0x29 and the achieved objective equals it (<c>cmp r1,r0</c> 0x005bf01a), stamp the <b>last-run</b>
    /// clock <c>+0x30 = now</c> (<c>str r0,[r4,#0x30]</c> 0x005bf028) - the stamp
    /// <c>EvaluateRepetitionPenalty</c> 0x005beee6 reads. Returns whether it stamped.
    /// </summary>
    // fidelity: M8-003
    public bool HandleBehaviorObjective(BehaviorObjective achieved, double nowSec)
    {
        if (BehaviorObjective == BehaviorObjective.Invalid || achieved != BehaviorObjective) return false;
        Context?.Penalty?.Ran(Id, nowSec);   // +0x30 = now (str r0,[r4,#0x30] 0x005bf028)
        return true;
    }

    /// <summary>
    /// <c>IBehavior::IsRunnableScored</c> 0x005bda28: 1 when <c>now &gt;= +0x118</c>, else 0
    /// (<c>vldr s0,[r4,#0x118]</c> 0x005bda38; <c>vcmpe.f32</c> 0x005bda3e; <c>movpl r0,#1</c> 0x005bda48).
    /// It has <b>no</b> engine caller and is not in the IBehavior vtable, so no chooser and not the manager
    /// consults it; this is the exported function only.
    /// </summary>
    // fidelity: M8-001
    public bool IsRunnableScored(double nowSec) => (float)nowSec >= _resumeSuppressionUntil;

    /// <summary>
    /// <c>IBehavior::StopWithoutImmediateRepetitionPenalty</c> 0x005beea0: <c>IBehavior::Stop()</c> first
    /// (<c>blx</c> 0x005beea4 - the running flag cleared, <c>StopInternal</c>, the <c>+0x30</c> last-run stamp,
    /// <c>StopActing(0,0)</c> and the scope's undo), then <c>+0x108 = now + 1.0f</c> on the shared repetition
    /// history (<c>vadd.f32</c>/<c>vstr</c> 0x005beeb0..0x005beebc). Only the M7/M15 concrete behaviours call
    /// this; nothing in M8 does. The reason the full <c>Stop</c> carries is this stack's own notion; the
    /// inventory gives none, so it is <see cref="BehaviorStopReason.Interrupted"/>, the stack's name for stopped
    /// without the penalty.
    /// </summary>
    // fidelity: M8-002
    public void StopWithoutImmediateRepetitionPenalty()
    {
        double now = Context.ClockSec?.Invoke() ?? Clock() / 1000.0;
        // IBehavior::Stop() (0x005beea4) takes no reason in the engine; BehaviorStopReason is this stack's own notion and the inventory gives none for this internal Stop.
        ReportMissing("M8-002: the stop reason of StopWithoutImmediateRepetitionPenalty's internal IBehavior::Stop() (0x005beea4) is not in the inventory (the engine's Stop takes none); BehaviorStopReason.Interrupted is the stack's own name");
        Stop(BehaviorStopReason.Interrupted);               // IBehavior::Stop 0x005beea4
        Context.Penalty?.Ran(Id, now);                      // Stop's +0x30 = now (0x005bd11e)
        Scope?.Dispose();                                   // Stop's undo (a)-(d) (0x005bd12c..0x005bd1c6)
        Context.Penalty?.StopWithoutImmediateRepetitionPenalty(Id, now);   // +0x108 = now + 1.0f
    }

    /// <summary>
    /// The engine's <c>IBehavior::Init</c> 0x005bcb54, on the parts this stack models, in the engine's order:
    /// the spark gate +0xd8 (from the AI process, <c>robot+0x44-&gt;+0x58</c>/<c>+0x5c</c>, an interface to that
    /// layer), halfword 0x0100 at +0xa0 (+0xa0 = 0, +0xa1 = 1), +0x98 cleared, +0x34 = now, then
    /// <c>vtable+0x48</c> = <c>InitInternal</c> (<see cref="OnStart"/>): a non-zero result
    /// (<see cref="InitFailed"/>) clears +0xa1, a zero one increments +0x80 (0x005bcd00..0x005bcd12); then the
    /// <c>SparkBehaviorDisables</c> lock (<see cref="SparkBehaviorDisables"/>) and +0x114 = 0 (0x005bcd58).
    /// </summary>
    // fidelity: M8-001
    protected void InitLifecycle(BehaviorContext context, double nowSec, bool sparkDisabled)
    {
        _sparkDisabled = sparkDisabled;          // +0xd8
        _engineActingFlag = false;               // halfword 0x0100 at +0xa0: +0xa0 = 0
        _engineRunning = true;                   //                             +0xa1 = 1
        _runningPenaltyClockSec = nowSec;        // +0x34 = now
        _sharedHandle = null;
        Interlocked.Increment(ref _callbackEpoch);   // +0x98 destroyed (0x005bccc6..0x005bcce6)
        InitFailed = false;
        OnStart();                               // vtable+0x48 InitInternal (0x005bccfe/0x005bcd00)
        if (InitFailed) _engineRunning = false;  // non-zero: +0xa1 = 0 (0x005bcd06)
        else _scoreIncreaseCount++;              // zero: +0x80++ (0x005bcd0c)
        SparkBehaviorDisables();                 // the "SparkBehaviorDisables" lock
        _resumeCount = 0;                        // +0x114 = 0
    }

    /// <summary>
    /// The <c>SparkBehaviorDisables</c> lock seam: the engine takes it when the unlock id +0x70 is not 0x55
    /// and equals <c>robot+0x44-&gt;+0x58</c>. The progression/AI-process layer owns those values.
    /// </summary>
    // fidelity: M7-014
    /// <summary>
    /// <c>IBehavior::Init</c> 0x005BCD16..0x005BCD44 and <c>IBehavior::Resume</c> 0x005BCFAC..0x005BCFDE: when
    /// <c>this+0x70</c> (the unlock/spark id, 0x55 = none) is not 0x55 and equals
    /// <c>[[this+0x2C]+0x44]+0x58</c> (the BehaviorManager's active spark), take the lock
    /// <c>"SparkBehaviorDisables"</c> (literal 0x00BF2B0A) with table 0x00C65F90 (only ObjectPositionUpdated)
    /// through <c>SmartDisableReactionsWithLock</c>; <c>IBehavior::Stop</c> releases it with the scope. Both ids
    /// belong to the spark/progression layer (M15), which this stack does not have, so they are seams
    /// (<see cref="UnlockIdentifier"/>, <see cref="ActiveSparkIdentifier"/>); without them the test cannot be made
    /// and the gap is reported, not answered.
    /// </summary>
    protected virtual void SparkBehaviorDisables()
    {
        if (UnlockIdentifier is not { } unlock || ActiveSparkIdentifier is not { } active)
        {
            ReportMissing("IBehavior::Init/Resume 'SparkBehaviorDisables' lock (0x005BCD16..0x005BCD44, 0x005BCFAC..0x005BCFDE): this+0x70 and [[this+0x2C]+0x44]+0x58 (the active spark) have no source in this stack (M15 spark layer); the lock is not taken");
            return;
        }
        int id = unlock();
        if (id == NoUnlockId) return;                                   // 0x005BCD18 cmp r0,#0x55
        if (id != active()) return;                                     // 0x005BCD22 cmp r0,r1
        Scope.SmartDisableReactionsWithLock(ReactionLockTables.SparkBehaviorDisablesName, ReactionLockTables.SparkBehaviorDisables);
    }

    /// <summary>The "none" value of IBehavior +0x70 and of the manager's active spark +0x58 (0x55).</summary>
    public const int NoUnlockId = 0x55;
    /// <summary>IBehavior +0x70: the behaviour's unlock/spark id (config). Null: not supplied (MISSING).</summary>
    public Func<int>? UnlockIdentifier { get; set; }
    /// <summary>BehaviorManager +0x58 (read as <c>[[this+0x2C]+0x44]+0x58</c>): the active spark id. Null: not supplied (MISSING).</summary>
    public Func<int>? ActiveSparkIdentifier { get; set; }

    // fidelity: M7-021
    private string _stateName = "";            // IBehavior +0x58 (a std::string, empty from the constructor)

    /// <summary>IBehavior +0x58: the state name the last <see cref="SetStateName"/> stored.</summary>
    public string StateName => _stateName;

    /// <summary>
    /// The state-name setter 0x005C0CA8 (<c>IBehavior::&lt;setter&gt;(const std::string&amp; newState)</c>), the one
    /// 175 call sites reach: <c>sChanneledInfoF("Behaviors", "Behavior.TransitionToState", {}, "Behavior:%s,
    /// FromState:%s ToState:%s", EnumToString(BehaviorID at +0x3C), old +0x58, new)</c> (0x005C0CD6..0x005C0CEC),
    /// then <c>std::string::assign</c> into +0x58 (0x005C0D12..0x005C0D16). The log shows the previous value as
    /// FromState; nothing branches on the string. The only readers of +0x58 outside logs are in
    /// <c>Robot::Update</c> (0x00513F6A..0x00513F9C): it builds <c>robot+0x4C</c> and sends it to
    /// <c>VizManager::SetText</c> and <c>SetSdkStatus(Behavior)</c>; those are M11/M12 and not built here (MISSING).
    /// </summary>
    protected void SetStateName(string newState)
    {
        // fidelity: M7-021
        // The "Behavior:%s" argument is EnumToString(BehaviorID at +0x3C) (0x005c0cb8/0x005c0cbc), not a free string: the BehaviorID's own name. A behaviour whose id is not one of the
        // BehaviorID names has no such value here (the engine's +0x3C is parsed from the config's behaviorID): the id string is printed and that is reported MISSING (once).
        string behaviorIdName = Enum.TryParse<BehaviorID>(Id, out var behaviorId) && Enum.IsDefined(behaviorId) ? behaviorId.ToString()
            : ReportMissingAndReturn(Id, $"IBehavior state-name log: behaviour id '{Id}' is not a BehaviorID name, so the engine's EnumToString(BehaviorID at +0x3C) (0x005c0cb8) has no value; the id string is printed");
        string line = $"info: [Behaviors] Behavior.TransitionToState: Behavior:{behaviorIdName}, FromState:{_stateName} ToState:{newState}";
        Log(line);
        Context?.Robot.Engine.Log(line);
        _stateName = newState;
        ReportMissing("Robot::Update's behaviour debug string (robot+0x4C = activity + ' ' + BehaviorID + '-' + IBehavior+0x58, 0x00513F6A..0x00513F9C) sent to VizManager::SetText and SetSdkStatus(Behavior): the sender and the SDK-status path are M11/M12 and not built");
    }

    /// <summary>
    /// <c>IBehavior::Update</c> 0x005bd074 returns 2 - before <c>UpdateInternal</c> runs - when byte +0xa0 is
    /// set and the current-action handle +0x84 is zero (<c>cbz r1</c> 0x005bd078; <c>cbz r1</c> 0x005bd07e;
    /// <c>movs r0,#2</c> 0x005bd088); otherwise it calls <c>vtable+0xc</c> (<c>UpdateInternal</c>). This is the
    /// first test of that, and <see cref="Update"/> applies it ahead of everything else. 0 means "run
    /// <c>UpdateInternal</c>".
    /// </summary>
    // fidelity: M8-001
    public int UpdateStatus() => _engineActingFlag && !HasCurrentAction ? 2 : 0;   // +0xa0 && +0x84 == 0

    /// <summary>
    /// <c>IBehavior::Resume</c> 0x005bceac. Returns true when the resume failed - the engine's non-zero
    /// return - which is either the TooManyResumesCliffOrMovement refusal or a non-zero
    /// <c>ResumeInternal</c>. Trigger 0 (CliffDetected) and 0x14 (UnexpectedMovement) count against +0x114
    /// (<c>cmp r5,#0x14</c> 0x005bcf16; <c>cmpne r5,#0</c> 0x005bcf1a; the counter is stored before the test,
    /// 0x005bcf1e..0x005bcf2c); from the second one on (the old value is &gt;= 1) it sets
    /// <c>+0x118 = now + 15.0f</c> (<see cref="TooManyResumesCooldownSec"/>, <c>vstr s0,[r4,#0x118]</c>
    /// 0x005bcf4c), triggers the emotion event <c>TooManyResumesCliffOrMovement</c> through the
    /// <c>MoodManager</c> (<c>TriggerEmotionEvent</c> 0x005bcf68; Confident -1.0 in
    /// <c>emotionevents/reaction_events.json</c>) and returns 1, so every such resume after the first is
    /// refused. The normal path sets +0xa2 = 1, stamps +0x34 = now, calls <c>vtable+0x4c</c>
    /// (<see cref="ResumeInternal"/>), clears +0xa2, and on a zero result sets +0xa1 = 1 and takes the
    /// SparkBehaviorDisables lock; on a non-zero one clears +0xa1 and returns it (0x005bcf80..0x005bcfde).
    /// The caller has attached the run (<see cref="AttachRun"/>) first, because the base
    /// <c>ResumeInternal</c> calls <c>InitInternal</c>.
    /// </summary>
    // fidelity: M8-001
    public bool Resume(ReactionTrigger? trigger, double nowSec)
    {
        if (trigger is ReactionTrigger.CliffDetected or ReactionTrigger.UnexpectedMovement)
        {
            int prior = _resumeCount;
            _resumeCount = prior + 1;
            if (prior >= 1)
            {
                _resumeSuppressionUntil = (float)nowSec + TooManyResumesCooldownSec;
                Context?.Mood?.Trigger(TooManyResumesEventName, nowSec);   // MoodManager::TriggerEmotionEvent
                return true;
            }
        }
        _resuming = true;                        // +0xa2 = 1
        _runningPenaltyClockSec = nowSec;        // +0x34 = now
        int result = ResumeInternal();           // vtable+0x4c
        _resuming = false;                       // +0xa2 = 0
        if (result != 0)
        {
            _engineRunning = false;              // +0xa1 = 0, and the result is Resume's
            return true;
        }
        _engineRunning = true;                   // +0xa1 = 1
        SparkBehaviorDisables();                 // Spark lock when +0x70 == robot+0x44->+0x58
        return false;
    }

    /// <summary>
    /// Gives a behaviour that is about to be resumed its run context. The engine's behaviour keeps its robot
    /// and scope state in itself; this stack makes the context and the scope per run, so the manager hands them
    /// over before <see cref="Resume"/>.
    /// </summary>
    internal void AttachRun(BehaviorContext context, BehaviorScope scope)
    {
        Context = context;
        Scope = scope;
    }

    /// <summary>
    /// The <c>vtable+0x4c ResumeInternal</c> slot; returns the engine's int (0 = resumed, non-zero = failed).
    /// <c>IBehavior::ResumeInternal</c> 0x005bda94 is: <c>IsRunnableBase</c> (0x005bda9c), the
    /// <c>vtable+0x50</c> test (0x005bdaaa), then <c>+0xa1 = 1</c> and a tail call of <c>vtable+0x48</c>
    /// (<c>InitInternal</c>, 0x005bdabc..0x005bdac4), else <c>movs r0,#1</c>. A class overrides it:
    /// <c>BehaviorPlayAnimSequence::ResumeInternal</c> 0x005bff1e returns 1.
    /// </summary>
    // fidelity: M8-001
    protected virtual int ResumeInternal()
    {
        if (!IsRunnableBase(Context)) return 1;
        if (!IsRunnableInternal(Context)) return 1;
        _engineRunning = true;                   // +0xa1 = 1 (0x005bdabc)
        BeginRun(Context, Scope);
        InitFailed = false;
        OnStart();                               // tail call vtable+0x48 InitInternal
        EndOfStart();
        return InitFailed ? 1 : 0;
    }

    /// <summary>
    /// <c>IBehavior::StopOnNextActionComplete</c> 0x005bd624..0x005bd6bb: sets the acting flag
    /// <c>+0xa0 = 1</c> (<c>strb.w r1,[r4,#0xa0]</c> 0x005bd698) and releases the <c>+0x98</c> handle
    /// (0x005bd694/0x005bd6b4). Its callers are the M7/M15 concrete behaviours; <c>Update</c> then
    /// returns 2 once the current action handle <c>+0x84</c> is clear.
    /// </summary>
    // fidelity: M8-001
    public void StopOnNextActionComplete()
    {
        _engineActingFlag = true;   // +0xa0 = 1 (0x005bd698)
        _sharedHandle = null;
        // +0x98 (the completion callback of the current action) is destroyed (0x005bd694..0x005bd6b4): the action still ends and
        // clears +0x84, but nothing is called back; UpdateInternal then returns 2.
        Interlocked.Increment(ref _callbackEpoch);
    }

    /// <summary>IBehavior +0x98 as a test-visible marker: Init and StopOnNextActionComplete destroy the completion callback (see <see cref="_callbackEpoch"/>).</summary>
    public object? SharedHandle => _sharedHandle;

    /// <summary>Sets the +0x98 handle (nothing in M8 sets it; the engine's action code does).</summary>
    protected void SetSharedHandle(object? handle) => _sharedHandle = handle;

    public virtual double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        BeginRun(context, scope);
        InitLifecycle(context, context.ClockSec?.Invoke() ?? Clock() / 1000.0, SparkGate?.Invoke() ?? false);
        EndOfStart();
        return Task.CompletedTask;
    }

    /// <summary>The per-run reset a start or a resume begins with.</summary>
    private void BeginRun(BehaviorContext context, BehaviorScope scope)
    {
        Context = context;
        Scope = scope;
        _finished = false;
        StartedMs = null;
        ClearWaits();
        while (_pending.TryDequeue(out _)) { }
        lock (_gate) _trace.Clear();
    }

    /// <summary>After <c>InitInternal</c>: a behaviour with nothing in flight and no polling update is complete.</summary>
    private void EndOfStart()
    {
        if (!_finished && !Busy && !KeepsRunningWithoutAction) _finished = true;
    }

    /// <summary>
    /// The spark-gate input (<c>robot+0x44-&gt;+0x58</c>/<c>+0x5c</c>), an interface to the AI/progression
    /// layer. Null means the gate reads false, which is what a robot with no AI process attached means.
    /// </summary>
    public Func<bool>? SparkGate { get; set; }

    /// <summary>The engine's <c>InitInternal</c> (<c>vtable+0x48</c>): start the first action.</summary>
    protected abstract void OnStart();

    /// <summary>The engine's <c>UpdateInternal</c>, after queued completions and waits have been serviced.</summary>
    protected virtual void OnUpdate() { }

    /// <summary>The engine's <c>StopInternal</c>.</summary>
    protected virtual void OnStop(BehaviorStopReason reason) { }

    // fidelity: M8-001
    public bool Update(BehaviorContext context, double nowMs)
    {
        // IBehavior::Update 0x005bd074..0x005bd088: +0xa0 set and +0x84 clear returns 2 before UpdateInternal.
        if (UpdateStatus() == 2) return false;
        if (_finished) return false;
        NowMs = nowMs;
        StartedMs ??= nowMs;

        // Only the completions already queued when this tick began run now. A callback that starts an
        // action which fails at once re-queues its own completion, and draining that inline would spin.
        int queued = _pending.Count;
        while (queued-- > 0 && !_finished && _pending.TryDequeue(out var callback)) callback();

        if (!_finished && _onWaitDone is { } waitDone)
        {
            if (double.IsNaN(_waitDeadlineMs)) _waitDeadlineMs = nowMs + _waitRemainingMs;
            if (nowMs >= _waitDeadlineMs)
            {
                _onWaitDone = null; _waitDeadlineMs = double.NaN;
                waitDone();
            }
        }

        if (!_finished && _waitCondition is { } condition)
        {
            if (double.IsNaN(_conditionDeadlineMs)) _conditionDeadlineMs = nowMs + _conditionTimeoutMs;
            bool met = condition();
            if (met || nowMs >= _conditionDeadlineMs)
            {
                var done = _onConditionDone;
                _waitCondition = null; _onConditionDone = null; _conditionDeadlineMs = double.NaN;
                done?.Invoke(met);
            }
        }

        if (!_finished) OnUpdate();

        // TriggerAnimationAction's timeout: IAction::IsDone returns true once now passes +0x74. The
        // deadline is armed on the first tick after the play starts.
        if (!_finished && _acting && _onActionTimeout is { } timeoutDone && !double.IsNaN(_actionTimeoutMs))
        {
            bool timedOut = false;
            lock (_gate)
            {
                if (double.IsNaN(_actionDeadlineMs)) _actionDeadlineMs = nowMs + _actionTimeoutMs;
                if (nowMs >= _actionDeadlineMs)
                {
                    _onActionTimeout = null; _actionTimeoutMs = double.NaN; _actionDeadlineMs = double.NaN;
                    timedOut = true;
                }
            }
            if (timedOut)
            {
                // outside _gate: StopActing may reach the helper component, whose lock order is component, then _gate (M8-011)
                Log("action timed out");
                StopActing();
                // the action ended with TIMEOUT 0x03000018 (IAction::UpdateInternal 0x00540d1c)
                _pending.Enqueue(() => timeoutDone(ActionOutcome.Failed(ActionOutcome.TimedOut)));
            }
        }

        EndOfStart();
        return !_finished;
    }

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        // fidelity: M8-011
        // IBehavior::Stop's own helper stop, first (0x005bd0f4..0x005bd108: +0xc8 set and [ctl+4]+1 != 0, then IBehavior::StopHelperWithoutCallback, no log), before +0xa1 is
        // cleared (0x005bd10c), before StopInternal (0x005bd114) and before StopActing(0,0) (0x005bd126), whose own gate then fires only if the helper is still owned.
        if (HasLiveHelper) StopHelperWithoutCallback();
        _engineRunning = false;                  // IBehavior::Stop clears +0xa1 (0x005bd10c)
        OnStop(reason);                          // vtable+0x54 StopInternal (0x005bd110/0x005bd114)
        StopActing(keepAction: false, viaCallback: false);   // IBehavior::Stop calls StopActing(0,0) 0x005bd126
    }

    /// <summary>Ends the behaviour of its own accord: the engine's behaviour running out of actions.</summary>
    protected void Finish()
    {
        if (_finished) return;
        _finished = true;
        Log("done");
    }

    /// <summary>
    /// The engine's <c>IBehavior::StopActing(keepAction, viaCallback)</c> 0x005bd34c: calls
    /// <c>vtable+0x80(0)</c> = <c>ScoredActingStateChanged(false)</c> (<c>blx r2</c> 0x005bd360), which clears
    /// the +0x104 running-score bonus; when <c>viaCallback</c> is false and a helper is
    /// live it logs "Stopping behavior helper because action stopped without callback" and calls
    /// <c>StopHelperWithoutCallback</c> (0x005bd39c/0x005bd3d6); then, if the current-action handle
    /// <c>+0x84</c> is set, it clears it before the cancel unless <paramref name="keepAction"/>
    /// (0x005bd3e0/0x005bd3e4), cancels it in <c>ActionList</c> (<c>ActionList::Cancel</c> 0x005bd3f0) and clears
    /// it afterwards when it is still the handle that was cancelled (0x005bd3f4..0x005bd3fe). This stack
    /// cancels by advancing the action epoch and dropping the queued completion; the helper path is the
    /// unowned BehaviorHelperComponent.
    /// </summary>
    // fidelity: M8-001
    protected void StopActing(bool keepAction, bool viaCallback)
    {
        ScoredActingStateChanged(false);                     // vtable+0x80(0)
        // fidelity: M8-011
        // 0x005bd362 cbnz viaCallback; 0x005bd364..0x005bd36e: only a live helper (+0xc8 set, [ctl+4]+1 != 0) is stopped, with the log at 0x005bd376..0x005bd39c.
        if (!viaCallback && HasLiveHelper)
        {
            Context?.Robot.Engine.Log($"info: [Behaviors] {Id}.StopActing.WithoutCallback.StopHelper: Stopping behavior helper because action stopped without callback");
            StopHelperWithoutCallback();
        }
        int handle = Volatile.Read(ref _currentActionHandle);
        ClearWaits();
        lock (_gate) _actionEpoch++;
        while (_pending.TryDequeue(out _)) { }
        if (handle != 0 && !keepAction) Interlocked.CompareExchange(ref _currentActionHandle, 0, handle);
        StopOwnAnimation();                                  // ActionList::Cancel
        if (handle != 0) Interlocked.CompareExchange(ref _currentActionHandle, 0, handle);   // 0x005bd3f4..0x005bd3fe
    }

    /// <summary>The parameterless convenience: <c>StopActing(0,0)</c>.</summary>
    protected void StopActing() => StopActing(keepAction: false, viaCallback: false);

    // fidelity: M8-011
    // IBehavior +0xc4/+0xc8: the weak reference to the helper this behaviour delegated to (SmartDelegateToHelper 0x005bec72/0x005bec7a). It belongs to the behaviour,
    // not to a run, so a Stop does not clear it (nothing in IBehavior::Stop does).
    private HelperWeakRef? _helperWeak;

    /// <summary>Whether the weak reference at +0xc8 is set and still has an owner (<c>[ctl+4] + 1 != 0</c>, 0x005bd364..0x005bd36e).</summary>
    public bool HasLiveHelper => _helperWeak is { IsLive: true };

    /// <summary>The helper's weak reference (+0xc4/+0xc8), or null before any successful delegation.</summary>
    public HelperWeakRef? HelperWeakReference => _helperWeak;

    /// <summary>
    /// <c>IBehavior::SmartDelegateToHelper(Robot&amp;, shared_ptr&lt;IHelper&gt;, onSuccess, onFailure)</c> 0x005beb10, in the engine's order: (1)/(2) the id is
    /// the behaviour name plus ".SmartDelegateToHelper" and "Behavior requesting to delegate to helper %s" is logged with the helper's name (0x005beb26..0x005beb6e);
    /// (3) a weak reference that is set and still live warns "IBehavior.SmartDelegateToHelper: Attempted to start a handler while handle already running, stopping
    /// running helper" and calls <see cref="StopHelperWithoutCallback"/> (0x005beba4..0x005bebe8); (4) the component is <c>[[[this+0x2c]+0x264]+0x10]</c>
    /// (0x005bebee..0x005bebf8): <c>Context.AI.Helpers</c>; (5) both callbacks are copied and <c>DelegateToHelper</c> is called (0x005bec2c); (6) on true the helper
    /// pointer and a weak reference to its control block are stored AFTER that call returns (0x005bec72..0x005bec80), on false the id name + "SmartDelegateToHelper.Failed"
    /// logs "Failed to delegate to helper" (0x005bec8c..0x005becb0); the bool is returned. The caller still holds its own handle during the call and releases it afterwards,
    /// as the engine's local <c>shared_ptr</c> is destroyed when the caller returns: a helper that already ran to completion inside the push therefore has no owner once
    /// the caller lets go, and the next call skips the warn/stop (extraction 5.2).
    /// </summary>
    protected bool SmartDelegateToHelper(HelperRef helper, Action<CozmoRobot>? onSuccess, Action<CozmoRobot>? onFailure)
    {
        var ai = Context?.AI ?? throw new NotSupportedException(
            "IBehavior::SmartDelegateToHelper 0x005bebee..0x005bebf8 reaches the BehaviorHelperComponent through [[[this+0x2c]+0x264]+0x10]; this behaviour's context has no AIComponent");
        var robot = Context.Robot;
        robot.Engine.Log($"info: [Behaviors] {Id}.SmartDelegateToHelper: Behavior requesting to delegate to helper {helper.Pointer.Name}");
        if (_helperWeak is { IsLive: true })
        {
            robot.Engine.Log($"warning: IBehavior.SmartDelegateToHelper: Attempted to start a handler while handle already running, stopping running helper");
            StopHelperWithoutCallback();
        }
        bool delegated = ai.Helpers.DelegateToHelper(robot, helper, onSuccess, onFailure);
        if (delegated) _helperWeak = new HelperWeakRef(helper);
        else robot.Engine.Log($"info: [Behaviors] {Id}SmartDelegateToHelper.Failed: Failed to delegate to helper");
        return delegated;
    }

    /// <summary>
    /// <c>IBehavior::StopHelperWithoutCallback()</c> 0x005bd21c: no weak reference (+0xc8 null) returns false (0x005bd228..0x005bd22e); <c>lock()</c> of it (0x005bd230)
    /// giving nothing returns false; a null helper pointer (+0xc4) returns false (0x005bd23a..0x005bd242); otherwise "Behavior stopping its helper" is logged
    /// (name + ".SmartStopHelper", 0x005bd24a..0x005bd26e), <c>BehaviorHelperComponent::StopHelperWithoutCallback</c> is called with the locked handle (0x005bd2b0;
    /// it acts only when that helper is the stack's bottom element) and its bool is returned after the lock is released.
    /// </summary>
    internal bool StopHelperWithoutCallback()
    {
        if (_helperWeak is not { } weak) return false;
        var locked = weak.Lock();
        if (locked is null) return false;
        try
        {
            var ai = Context?.AI ?? throw new NotSupportedException(
                "IBehavior::StopHelperWithoutCallback 0x005bd2a6..0x005bd2b0 reaches the BehaviorHelperComponent through [[[this+0x2c]+0x264]+0x10]; this behaviour's context has no AIComponent");
            Context.Robot.Engine.Log($"info: [Behaviors] {Id}.SmartStopHelper: Behavior stopping its helper");
            return ai.Helpers.StopHelperWithoutCallback(locked);
        }
        finally { locked.Release(); }
    }

    /// <summary><c>IBehavior::StopActing(keepAction, 1)</c> as <c>IHelper::StopActing</c> (0x005b6ec6) and <c>IHelper::Stop</c> (0x005b67e4) call it: viaCallback true.</summary>
    internal void StopActingFromHelper(bool keepAction) => StopActing(keepAction, viaCallback: true);

    /// <summary>
    /// <c>IBehavior::StartActing</c> 0x005bdacc, in its order: with +0xa0 set the action is deleted and it returns 0
    /// (0x005bdad4..0x005bdadc); with neither +0xa2 nor +0xa1 set it warns <c>IBehavior.StartActing.Failure.NotRunning</c> and returns 0
    /// (0x005bdae2..0x005bdaee); with +0x84 already set it warns <c>IBehavior.StartActing.Failure.AlreadyActing</c> and returns 0
    /// (0x005bdaf0..0x005bdb3e); only then the handle becomes +0x84 (<c>str.w r1,[r5,#0x84]</c> 0x005bdb50), the callback is stored
    /// (+0x98) and <c>vtable+0x80(1)</c> - <c>ScoredActingStateChanged</c>, which clears +0x104 whatever its argument - is called
    /// (<c>movs r1,#1</c> 0x005bdb54; <c>blx r2</c> 0x005bdb5c). <c>ActionList::QueueAction</c> follows, and its failure (warning
    /// <c>...Failure.NotQueued</c>, 0x005bdb6c..0x005bdba8) is not modelled: the stack's actions cannot fail to queue. Returns 0 on a refusal,
    /// else the new handle.
    /// </summary>
    // fidelity: M8-001
    internal int StartActing()
    {
        if (_engineActingFlag) { Log("IBehavior.StartActing: the action is dropped because the behaviour is stopping (+0xa0)"); return 0; }
        if (!_resuming && !_engineRunning) { Log("IBehavior.StartActing.Failure.NotRunning"); return 0; }
        if (HasCurrentAction) { Log("IBehavior.StartActing.Failure.AlreadyActing"); return 0; }
        int handle = Interlocked.Increment(ref _actionHandleCounter);
        Volatile.Write(ref _currentActionHandle, handle);
        ScoredActingStateChanged(true);
        return handle;
    }

    /// <summary>
    /// <c>IBehavior::HandleActionComplete</c> 0x005be1e6: when +0x84 is the completed action (0x005be1ec..0x005be1f6) it stores 0 to +0x84
    /// (0x005be1fc) and calls <c>vtable+0x80(0)</c> (<c>ScoredActingStateChanged</c>, 0x005be202..0x005be208) - so the +0x104 bonus is cleared -
    /// and only then, if +0xa1 is set and the callback (+0x98) is still there, calls it (0x005be20a..0x005be21c). This is the first half; whether
    /// the callback may be called is <see cref="CallbackMayRun"/>.
    /// </summary>
    // fidelity: M8-001
    internal void ActingEnded(int handle)
    {
        if (handle != 0 && Interlocked.CompareExchange(ref _currentActionHandle, 0, handle) == handle)
            ScoredActingStateChanged(false);
    }

    /// <summary>The second half of <c>HandleActionComplete</c>: the callback runs only while +0xa1 is set and +0x98 was not destroyed since the action started.</summary>
    protected bool CallbackMayRun(int epochAtStart) => _engineRunning && epochAtStart == Volatile.Read(ref _callbackEpoch);

    /// <summary>
    /// The engine's <c>TriggerAnimationAction</c> as a behaviour starts it with <c>StartActing</c>: resolve the
    /// trigger through the shipped map, play it, and call <paramref name="onDone"/> when it ends. A trigger that
    /// resolves to nothing is reported and its callback still runs, on the next tick rather than inline so a
    /// failing action cannot recurse. This stack runs the completion callback for every ended action,
    /// including a failed one (for TRACKS_LOCKED the engine does: <c>DeleteActionAndIter</c> 0x0053f9e4 calls <c>PrepForCompletion</c> 0x0053fa24), but - as <c>HandleActionComplete</c> 0x005be1e6 does - only while the behaviour's +0xa1 is set and its callback has not been destroyed.
    ///
    /// The track mask is the action's own <c>tracksToLock</c> (IActionRunner +0x54, the constructor's
    /// argument, 0x0053fe0e/0x0053fe14) and nothing else - not the tracks the clip happens to use. While
    /// <c>MovementComponent::AreAnyTracksLocked(mask)</c> (0x00540440) is true <c>IActionRunner::Update</c>
    /// 0x00540370 ends the action with <c>0x03000019</c> (TRACKS_LOCKED, 0x00540572..0x0054057c) and does not
    /// retry it (<c>RetriesRemain</c> 0x00540836 has only the compound actions as callers, and
    /// <c>ActionQueue::Update</c> 0x0053f5f4 deletes any action whose result is not RUNNING); otherwise it takes
    /// <c>MovementComponent::LockTracks(mask, to_string(action id), name)</c> through 0x004f0f4c, which sends
    /// <c>DisableAnimTracks</c> for the tracks that were free, and the end of the action releases it, which sends
    /// <c>EnableAnimTracks</c>. A mask of 0 locks nothing and sends nothing.
    /// </summary>
    /// <param name="alsoLock">The action's <c>tracksToLock</c>. <c>BehaviorReactToUnexpectedMovement</c> sets 4 (the body) when the movement came from behind.</param>
    // fidelity: M8-007
    protected void PlayTrigger(AnimationTrigger trigger, Action onDone, AnimationTrack alsoLock = AnimationTrack.None,
                               double? timeoutSec = null)
    {
        int handle = StartActing();
        if (handle == 0) return;                                              // StartActing refused (it logged why)
        RunTriggerAction(handle, trigger, _ => onDone(), alsoLock, timeoutSec, numLoops: 1, liftSafe: false);
    }

    /// <summary>The 60.0f timeout the reaction and PlayAnim animations pass (<c>movt r1,#0x4270</c> 0x005c017e; 0x42700000).</summary>
    public static readonly float TriggerAnimationTimeoutSec = BitConverter.Int32BitsToSingle(0x42700000);

    /// <summary>
    /// One <c>TriggerAnimationAction</c> / <c>TriggerLiftSafeAnimationAction</c> run: the action itself, started
    /// by the caller's <c>StartActing</c> (<paramref name="handleToClear"/> is that handle, or 0 for a child of a
    /// compound action whose own handle the caller holds). <paramref name="numLoops"/> is the constructor's
    /// <c>numLoops</c>: the streamer plays the animation that many times inside this one action, and 0 loops
    /// until it is cancelled (<c>AnimationStreamer::Update</c> 0x0057d24e..0x0057d25a); the
    /// <c>PlayAnimationAction</c> constructor turns a 60.0f timeout into FLT_MAX when numLoops is 0
    /// (0x00543c96..0x00543cb2). <paramref name="liftSafe"/> is the <c>TriggerLiftSafeAnimationAction</c>
    /// constructor 0x00544710: LIFT (2) is ORed into the mask only while the robot is carrying
    /// (<c>[robot+0x284]+8 != -1</c>) and <c>robot+0x355</c> (the off-treads state) is 0 (0x00544714..0x00544732).
    /// </summary>
    // fidelity: M8-007, M8-005
    internal void RunTriggerAction(int handleToClear, AnimationTrigger trigger, Action<ActionOutcome> done,
                                   AnimationTrack tracksToLock, double? timeoutSec, int numLoops, bool liftSafe)
    {
        int epoch = Volatile.Read(ref _callbackEpoch);
        void Complete(ActionOutcome outcome)
        {
            ActingEnded(handleToClear);
            _pending.Enqueue(() => { if (CallbackMayRun(epoch)) done(outcome); });
        }

        if (liftSafe && Context.Robot.Motion.IsCarryingObject?.Invoke() == true
            && Context.Robot.Sensors.OffTreadsState == OffTreadsState.OnTreads)
            tracksToLock |= AnimationTrack.Lift;                             // 0x00544714..0x00544732
        byte mask = CozmoMotion.MaskFor(tracksToLock);
        if (mask != 0 && Context.Robot.Motion.AreAnyTracksLocked(mask))
        {
            // IActionRunner::Update 0x00540370: AreAnyTracksLocked 0x00540440; the action ends 0x03000019
            // (0x00540572..0x0054057c) after the two warnings (0x005404f2..0x00540530). No retry; the completion callback still runs
            // (DeleteActionAndIter 0x0053f9e4 calls PrepForCompletion, 0x0053fa24).
            Log($"{trigger}: warning: Action not running because required tracks are locked (result 0x{ActionOutcome.TracksLocked:X8})");
            Complete(ActionOutcome.Failed(ActionOutcome.TracksLocked));
            return;
        }
        // IActionRunner::Update takes the lock (LockTracks 0x00540584..0x0054058e, owner = to_string(id)) before the action's own Init
        // (0x00540592), so it is held - and DisableAnimTracks sent - even when Init then fails.
        string? owner = null;
        if (mask != 0)
        {
            owner = Context.Robot.Motion.NextActionTag();
            Context.Robot.Motion.LockTracks(mask, owner);
        }
        void Fail(uint code)
        {
            if (owner is not null) Context.Robot.Motion.UnlockTracks(mask, owner);   // the action's end releases it (0x0054121e..0x0054122a)
            Complete(ActionOutcome.Failed(code));
        }

        var lib = Context.Robot.Animations.Library;
        if (lib is null) { Log($"{trigger}: no animation assets are loaded"); Fail(ActionOutcome.NoAnimationForTrigger); return; }

        var resolved = Context.Triggers.Resolve(trigger, lib, Context.Random);
        if (!resolved.Resolved)
        {
            Log($"{trigger}: {resolved.Problem ?? "resolved to no animation"}");
            Fail(ActionOutcome.NoAnimationForTrigger);                       // TriggerAnimationAction::Init 0x0054443c, 0x00544444..0x00544448
            return;
        }

        // PlayAnimationAction constructor 0x00543c08: numLoops 0 and the 60.0f default become FLT_MAX.
        double? timeout = timeoutSec;
        if (numLoops == 0 && timeoutSec is { } t60 && (float)t60 == TriggerAnimationTimeoutSec) timeout = float.MaxValue;

        var ticket = Context.Robot.Animations.PlayTracked(resolved.Selected!, lockTracks: AnimationTrack.None, numLoops: numLoops,
                                                          heldLockMask: mask, heldLockOwner: owner);
        if (ticket is null)
        {
            Log($"{trigger} -> {resolved.Selected}: the animation could not be started");
            Fail(ActionOutcome.StreamRefused);                               // SetStreamingAnimation refused: 0x03000001
            return;
        }

        Log($"play {trigger} -> {resolved.Selected}");
        OnTriggerResolved(trigger, resolved.Selected!);
        int actionEpoch;
        lock (_gate)
        {
            _animations = Context.Robot.Animations;
            _generation = ticket.Generation;
            _owns = true;
            _acting = true;
            actionEpoch = _actionEpoch;
            // TriggerAnimationAction's timeout_s: the deadline is computed on the first Update after the
            // play starts, because the engine starts it on the manager's clock.
            _actionTimeoutMs = timeout is { } t ? t * 1000 : double.NaN;
            _actionDeadlineMs = double.NaN;
            _onActionTimeout = timeout is null ? null : (o => { if (CallbackMayRun(epoch)) done(o); });
        }
        ticket.Completion.ContinueWith(completion =>
        {
            lock (_gate)
            {
                if (actionEpoch != _actionEpoch) return;
                // PlayAnimationAction has no end reason: animEnded sets +0x89 and CheckIfDone returns SUCCESS (0x0054541d..0x005454ee, 0x005441c0).
                // What a replaced or aborted tag does is not settled by the inventory, so every end of the animation is a success here.
                ReportMissing("PlayAnimationAction: what the streamer reports for a replaced or aborted animation tag is not in the inventory; every end of the animation is treated as animEnded (success)");
                // +0x84 is cleared first (HandleActionComplete 0x005be1fc, before the callback is reachable, 0x005be216); the behaviour still
                // reads as busy (_acting) until the completion is queued, so neither a callback nor Update can see a gap.
                ActingEnded(handleToClear);
                _pending.Enqueue(() => { if (CallbackMayRun(epoch)) done(ActionOutcome.Succeeded); });
                _owns = false; _acting = false;
                _onActionTimeout = null; _actionTimeoutMs = double.NaN; _actionDeadlineMs = double.NaN;
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Called when an action's trigger has resolved to a clip and the clip has started. A class that records the choice overrides it.</summary>
    protected virtual void OnTriggerResolved(AnimationTrigger trigger, string clip) { }

    /// <summary>
    /// The engine's <c>WaitAction</c> started on its own (<c>IBehavior::StartActing</c>, as <c>ReactToReturnedToTreads::InitInternal</c> 0x00608500,
    /// <c>DriveInDesperation::TransitionToIdle</c> 0x005d8ef0, <c>KnockOverCubes</c> 0x005c34a8, <c>PounceOnMotion</c> 0x005f8ed4, <c>ReactToPickup</c>
    /// 0x00607750 and <c>ReactToMotorCalibration</c> 0x006065f0 do): it is the current action (+0x84) until it ends, and its callback is the
    /// +0x98 one, so <c>StopOnNextActionComplete</c> destroys it. A wait that runs beside another action in one <c>CompoundActionParallel</c> is
    /// <see cref="StartParallel"/>. Call back after this many seconds of the manager's clock.
    /// </summary>
    protected void Wait(double seconds, Action onDone)
    {
        int handle = StartActing();
        if (handle == 0) return;
        int epoch = Volatile.Read(ref _callbackEpoch);
        WaitCore(seconds, () => { ActingEnded(handle); if (CallbackMayRun(epoch)) onDone(); });
    }

    private void WaitCore(double seconds, Action onDone)
    {
        _waitRemainingMs = seconds * 1000;
        // Started from a transition inside Update the wait begins now; started from OnStart, before the
        // manager has ticked, it begins at the first tick.
        _waitDeadlineMs = StartedMs is null ? double.NaN : NowMs + _waitRemainingMs;
        _onWaitDone = onDone;
        Log($"wait {seconds:F1} s");
    }

    /// <summary>
    /// The engine's <c>WaitForLambdaAction</c>, a standalone action like <see cref="Wait"/>: call back when the condition holds, or with false when the
    /// timeout passes first.
    /// </summary>
    protected void WaitUntil(Func<bool> condition, double timeoutSec, Action<bool> onDone, string what)
    {
        int handle = StartActing();
        if (handle == 0) return;
        int epoch = Volatile.Read(ref _callbackEpoch);
        _waitCondition = condition;
        _onConditionDone = met => { ActingEnded(handle); if (CallbackMayRun(epoch)) onDone(met); };
        _conditionTimeoutMs = timeoutSec * 1000;
        _conditionDeadlineMs = StartedMs is null ? double.NaN : NowMs + _conditionTimeoutMs;
        Log($"wait for {what} (up to {timeoutSec:F1} s)");
    }

    /// <summary>
    /// A <c>CompoundActionParallel</c> started with one <c>StartActing</c> (one +0x84, one callback): it ends when all of its
    /// <paramref name="children"/> have ended (<see cref="ParallelAction.ChildDone"/>), and then calls <paramref name="onDone"/> once, under the
    /// same +0xa1 and +0x98 rules as any action's callback. Returns null if <c>StartActing</c> refused.
    /// </summary>
    protected ParallelAction? StartParallel(int children, Action onDone)
    {
        int handle = StartActing();
        if (handle == 0) return null;
        return new ParallelAction(this, handle, children, Volatile.Read(ref _callbackEpoch), onDone);
    }

    /// <summary>The wait child of a parallel action (a <c>WaitAction</c> inside the compound).</summary>
    protected void WaitInParallel(ParallelAction action, double seconds) => WaitCore(seconds, () => action.ChildDone(ActionOutcome.Succeeded));

    /// <summary>
    /// The <c>WaitForLambdaAction</c> child of a parallel action (<c>WaitForLambdaAction</c> 0x0055B554: the function at +0x78, the timeout at
    /// +0x90; <c>CheckIfDone</c> 0x0055DADE: true is success, false is still running; the timeout fails it with 0x03000018). When
    /// <paramref name="ignoreFailure"/> is set (the <c>AddAction(action, ignoreFailure = true, ..)</c> argument, 0x00605044 r3 = 1) a failed child does not
    /// end the compound. Which of the compound's results an ignored failure yields is not in the inventory; here it counts as the child having ended.
    /// </summary>
    protected void WaitUntilInParallel(ParallelAction action, Func<bool> condition, double timeoutSec, bool ignoreFailure)
    {
        _waitCondition = condition;
        _onConditionDone = met => action.ChildDone(met ? ActionOutcome.Succeeded : ActionOutcome.Failed(ActionOutcome.TimedOut), ignoreFailure);
        _conditionTimeoutMs = timeoutSec * 1000;
        _conditionDeadlineMs = StartedMs is null ? double.NaN : NowMs + _conditionTimeoutMs;
        Log($"wait for a condition in parallel (up to {timeoutSec:F2} s)");
    }

    /// <summary>The +0x98 epoch an action captures when it starts (see <see cref="CallbackMayRun"/>).</summary>
    protected int CallbackEpoch => Volatile.Read(ref _callbackEpoch);

    /// <summary>The animation child of a parallel action (a <c>TriggerAnimationAction</c> inside the compound).</summary>
    protected void PlayTriggerInParallel(ParallelAction action, AnimationTrigger trigger, AnimationTrack alsoLock = AnimationTrack.None, double? timeoutSec = null) =>
        RunTriggerAction(0, trigger, o => action.ChildDone(o), alsoLock, timeoutSec, numLoops: 1, liftSafe: false);

    /// <summary>Cancels the children of a parallel action that ended early: the wait is cleared and the animation stopped.</summary>
    private void CancelParallelChildren()
    {
        ClearWaits();
        lock (_gate) _actionEpoch++;
        StopOwnAnimation();
    }

    /// <summary>One compound parallel action's bookkeeping.</summary>
    protected sealed class ParallelAction
    {
        private readonly SteppedBehavior _owner;
        private readonly int _handle, _epoch;
        private readonly Action _onDone;
        private int _remaining;
        internal ParallelAction(SteppedBehavior owner, int handle, int children, int epoch, Action onDone)
        { _owner = owner; _handle = handle; _remaining = children; _epoch = epoch; _onDone = onDone; }

        private bool _ended;

        /// <summary>
        /// A child ended with <paramref name="outcome"/>. <c>CompoundActionParallel::UpdateInternal</c> 0x0054fab0..0x0054fbd0 dispatches on the result's category
        /// (<c>tbb</c> 0x0054fb0e): a success lets the others finish, and the compound succeeds when none is running (0x0054fb64); a failure runs
        /// <c>ShouldIgnoreFailure</c> (0x0054f171), whose map is empty for the list constructor (0x0054e991; <c>AddAction</c> ignoreFailure 0, 0x0054ea34), so
        /// the <b>first failed child ends the compound at once</b> with its failure (0x0054fb36..0x0054fbcc) and the others are not awaited. Then
        /// <c>HandleActionComplete</c> 0x005be1e6 clears +0x84 and calls the callback for any result. The other children are cancelled here (the wait cleared,
        /// the animation stopped); that the engine's deletion of the compound cancels them the same way is not cited (MISSING). Called on the manager's thread.
        /// </summary>
        public void ChildDone(ActionOutcome outcome, bool ignoreFailure = false)
        {
            if (_ended) return;
            if (!outcome.Success && ignoreFailure) outcome = ActionOutcome.Succeeded;   // ShouldIgnoreFailure (0x0054f171): the child ended, the compound goes on
            bool last = --_remaining <= 0;
            if (outcome.Success && !last) return;
            _ended = true;
            if (!outcome.Success)
            {
                ReportMissing("CompoundActionParallel: cancelling the remaining children when one fails (0x0054fb36..0x0054fbcc) - how the engine's deletion cancels a running animation or wait is not in the inventory; they are cancelled here");
                _owner.CancelParallelChildren();
            }
            _owner.ActingEnded(_handle);
            if (_owner.CallbackMayRun(_epoch)) _onDone();
        }
    }

    /// <summary>
    /// The engine's <c>CalibrateMotorAction(head: true, lift: false)</c>: ask the robot to recalibrate the
    /// head and wait for its report that the calibration started and then finished.
    ///
    /// <c>CalibrateMotorAction::CheckIfDone</c> 0x00547D38 stays running until the motors it was asked for
    /// report calibrated, testing <c>Robot::IsHeadCalibrated</c> and <c>IsLiftCalibrated</c> against the
    /// two request flags at +0x78 and +0x79 and the two already-started flags at +0x7A and +0x7B.
    ///
    /// The action does time out, at 30.0 s. <c>IAction::IAction</c> 0x00540C44 writes -1.0f to +0x74
    /// (<c>movs r1, #0</c> 0x00540C9E / <c>movt r1, #0xbf80</c> 0x00540CA2 / <c>str r1,[r4,#0x74]</c>
    /// 0x00540CA6), but that is the "timer not started" sentinel: <c>IAction::UpdateInternal</c>
    /// 0x00540D1C arms it (0x00540D52/0x00540D5A/0x00540D64) and computes the deadline as
    /// <c>start + vtable[+0x2c]</c> (0x00540D94/0x00540D9E/0x00540DA2). <c>CalibrateMotorAction</c>
    /// inherits the default slot +0x2c (vtable vptr 0x0102194C, slot 0x01021978 -> thunk 0x0052B0C2)
    /// which returns <c>0x41F00000</c> = 30.0f, and on expiry the action fails with 0x03000018
    /// ("IAction.Update.TimedOut" 0x00540FE0 / "%s timed out after %.1f seconds." 0x00540FF8).
    /// </summary>
    // fidelity: M8-008
    protected void CalibrateHead(Action onDone)
    {
        // fidelity: M8-008
        ReportMissing("M8-008: CalibrateMotorAction (0x00547a9c, IAction) is not hosted by an action runner here: the stack's action tick (Motion.UpdateActions) hosts only head/lift MoveActions, so the head recalibration is a behaviour-side wait. The engine's done test is IsHeadCalibrated && +0x7A (0x00547d40..0x00547d80), this wait watches HeadCalibrating; the timeout is not an IAction deadline (float start + 30.0f, 0x00540d94..0x00540daa) and the 0x03000018 result is not delivered");
        var state = Context.Robot.State;
        bool started = false;
        Context.Robot.Motion.RequestMotorCalibration(head: true, lift: false);
        Log("calibrate head (StartMotorCalibration head=1 lift=0)");
        WaitUntil(() =>
        {
            if (state.HeadCalibrating) started = true;
            return started && !state.HeadCalibrating;
        }, CalibrationAllowanceSec, ok =>
        {
            // CalibrateMotorAction::CheckIfDone 0x00547d38 on completion: sChanneledInfoF("Unnamed", "CalibrateMotorAction.CheckIfDone.Done", {}, "") (0x00547d98..0x00547d9c).
            // IAction::UpdateInternal on expiry (0x00540e80..0x00540eb6): sWarningF("IAction.Update.TimedOut", "%s timed out after %.1f seconds.", the action's name, the 0x2c slot's 30.0f);
            // the name is "CalibrateMotor-" + "Head" for (head, !lift) (0x00547aaa..0x00547aca).
            Log(ok ? "info: [Unnamed] CalibrateMotorAction.CheckIfDone.Done"
                   : $"warning: IAction.Update.TimedOut: CalibrateMotor-Head timed out after {CalibrationAllowanceSec.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} seconds.");
            onDone();
        }, "head calibration");
    }

    /// <summary>
    /// The engine's <c>CalibrateMotorAction</c> timeout: the inherited <c>IAction</c> vtable[+0x2c] value
    /// 30.0 s (thunk 0x0052B0C2 returns 0x41F00000). See <see cref="CalibrateHead"/>.
    /// </summary>
    public const double CalibrationAllowanceSec = 30.0;

    /// <summary>
    /// Queues a completion to run on the next <see cref="Update"/>, on the manager's thread. Animation
    /// completions arrive this way; a subclass with another asynchronous action posts its completion here.
    /// </summary>
    protected void Post(Action callback) => _pending.Enqueue(callback);

    /// <summary>Writes one trace line.</summary>
    protected void Log(string line)
    {
        lock (_gate) _trace.Add(line);
        Step?.Invoke(line);
    }

    private void ClearWaits()
    {
        _onWaitDone = null; _waitDeadlineMs = double.NaN;
        _waitCondition = null; _onConditionDone = null; _conditionDeadlineMs = double.NaN;
    }

    private void StopOwnAnimation()
    {
        _acting = false;
        PlayAnimBehavior.StopOwnAnimation(ref _animations, ref _generation, ref _owns, _gate);
    }
}

/// <summary>
/// The three runnable-gate slots of every behaviour class's vtable (<c>IsRunnableBase</c> 0x005bd864..0x005bd89c calls <c>[vptr+0x20]</c> when robot+0x355 is set, <c>[vptr+0x24]</c>
/// when robot+0x34a is set and <c>[vptr+0x28]</c> when [robot+0x284]+8 != -1). Read from each class's vtable (<c>_ZTVN4Anki5Cozmo..Behavior..E</c>; slot = (relocation - vtable start - 8) / 4
/// with the relocation at vtable start + 8 + 0x20/0x24/0x28): each slot is a two-instruction <c>movs r0,#N; bx lr</c> (N = 0 or 1), except FindFaces and ExploreLookAroundInPlace whose +0x28 is
/// 0x005c1d76, <c>ldrb.w r0,[r0,#0x120]</c> (null here: the class supplies it). The base <c>IBehavior</c> vtable (0x010264e0) has 0x005bf04c (0), 0x0059ec12 (0) and
/// <c>__cxa_pure_virtual</c>. Keyed by <see cref="BehaviorClass"/> name (BehaviorWait is <c>Wait</c>'s vptr overwrite, GOT 0x0103EDF0).
/// </summary>
// fidelity: M8-001
internal static class EngineRunnableGates
{
    /// <summary>
    /// The three gate tests of <c>IBehavior::IsRunnableBase</c> (0x005bd864..0x005bd89c) for a behaviour that does not derive from <see cref="SteppedBehavior"/>, from the production
    /// sources: robot+0x355 (<c>Sensors.OffTreadsState</c> != OnTreads) -> slot +0x20, robot+0x34a (<c>Sensors.OnChargerPlatform</c>) -> +0x24, carrying (<c>Motion.IsCarryingObject</c>) -> +0x28.
    /// A running behaviour (its own +0xa1 flag, <paramref name="running"/>) is runnable before any gate. A class outside the table has the base's 0, 0 and a pure-virtual +0x28: refused, MISSING once.
    /// </summary>
    public static bool Allows(BehaviorContext context, string behaviorClass, bool running = false)
    {
        if (running) return true;                                                // IsRunnableBase's first test, +0xa1 (0x005bd780), before any gate (0x005bd864..0x005bd89c)
        bool known = Table.TryGetValue(behaviorClass, out var g);
        if (!known) SteppedBehavior.ReportMissing($"IsRunnableBase gates: class '{behaviorClass}' is not in the table of the 79 engine vtables; the base slots (0, 0, pure virtual) are answered, so it is refused wherever a gate is consulted");
        if (context.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads && !(known && g.Slot20)) return false;
        if (context.Robot.Sensors.OnChargerPlatform && !(known && g.Slot24)) return false;
        if (context.Robot.Motion.IsCarryingObject?.Invoke() == true && !(known && g.Slot28 == true)) return false;
        return true;
    }

    public static readonly IReadOnlyDictionary<string, (bool Slot20, bool Slot24, bool? Slot28)> Table = new Dictionary<string, (bool, bool, bool?)>
    {
        ["Bouncer"] = (false, false, false),
        ["BringCubeToBeacon"] = (false, false, true),
        ["BuildPyramid"] = (false, false, true),
        ["BuildPyramidBase"] = (false, false, true),
        ["CantHandleTallStack"] = (false, false, false),
        ["CheckForStackAtInterval"] = (false, false, false),
        ["CubeLiftWorkout"] = (false, false, true),
        ["Dance"] = (false, false, true),
        ["DevTurnInPlaceTest"] = (false, false, false),
        ["DockingTestSimple"] = (false, false, false),
        ["DriveInDesperation"] = (true, false, false),
        ["DriveOffCharger"] = (true, true, false),
        ["DrivePath"] = (false, false, false),
        ["DriveToFace"] = (false, false, false),
        ["EarnedSparks"] = (false, false, true),
        ["EnrollFace"] = (false, false, false),
        ["ExploreLookAroundInPlace"] = (false, false, null),
        ["ExploreVisitPossibleMarker"] = (false, false, false),
        ["ExpressNeeds"] = (false, false, false),
        ["FactoryCentroidExtractor"] = (false, false, true),
        ["FactoryTest"] = (false, false, true),
        ["FeedingEat"] = (false, false, false),
        ["FeedingSearchForCube"] = (false, false, false),
        ["FindFaces"] = (false, false, null),
        ["FireTruckAlarm"] = (false, false, false),
        ["FistBump"] = (false, false, true),
        ["GuardDog"] = (false, false, false),
        ["InteractWithFaces"] = (false, false, false),
        ["KnockOverCubes"] = (false, false, false),
        ["LiftLoadTest"] = (false, false, false),
        ["LookAround"] = (false, false, false),
        ["LookForFaceAndCube"] = (false, false, false),
        ["LookInPlaceMemoryMap"] = (false, false, false),
        ["OnConfigSeen"] = (false, false, false),
        ["OnboardingShowCube"] = (false, true, true),
        ["PeekABoo"] = (false, false, false),
        ["PickUpAndPutDownCube"] = (false, false, false),
        ["PickUpCube"] = (false, false, false),
        ["PlayAnim"] = (false, false, true),
        ["PlayAnimOnNeedsChange"] = (false, false, true),
        ["PlayAnimWithFace"] = (false, false, true),
        ["PlayArbitraryAnim"] = (false, false, true),
        ["PopAWheelie"] = (false, false, false),
        ["PounceOnMotion"] = (false, false, false),
        ["PutDownBlock"] = (false, false, true),
        ["PyramidThankYou"] = (false, false, false),
        ["RequestGameSimple"] = (false, false, true),
        ["RespondPossiblyRoll"] = (false, false, false),
        ["RespondToRenameFace"] = (false, false, false),
        ["RollBlock"] = (false, false, true),
        ["SearchForFace"] = (false, false, false),
        ["Singing"] = (false, false, false),
        ["StackBlocks"] = (false, false, true),
        ["ThinkAboutBeacons"] = (false, false, false),
        ["TrackLaser"] = (false, false, false),
        ["TurnToFace"] = (false, false, false),
        ["VisitInterestingEdge"] = (false, false, false),
        ["Wait"] = (true, false, true),
        ["AcknowledgeFace"] = (false, false, false),
        ["AcknowledgeObject"] = (false, false, true),
        ["RamIntoBlock"] = (false, false, true),
        ["ReactToCliff"] = (false, false, true),
        ["ReactToCubeMoved"] = (false, false, true),
        ["ReactToFrustration"] = (false, false, true),
        ["ReactToImpact"] = (false, false, false),
        ["ReactToMotorCalibration"] = (false, false, true),
        ["ReactToOnCharger"] = (true, true, true),
        ["ReactToPet"] = (false, false, false),
        ["ReactToPickup"] = (true, false, true),
        ["ReactToPlacedOnSlope"] = (true, false, true),
        ["ReactToPyramid"] = (false, false, false),
        ["ReactToReturnedToTreads"] = (true, false, true),
        ["ReactToRobotOnBack"] = (true, false, true),
        ["ReactToRobotOnFace"] = (true, false, true),
        ["ReactToRobotOnSide"] = (true, false, true),
        ["ReactToRobotShaken"] = (true, false, true),
        ["ReactToSparked"] = (true, false, false),
        ["ReactToStackOfCubes"] = (false, false, false),
        ["ReactToUnexpectedMovement"] = (false, false, true),
    };
}
